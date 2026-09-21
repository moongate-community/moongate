using Moongate.Core.Primitives;
using Npgsql;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Services;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Integration.Transactions;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceTransactionTests
{
    private readonly PostgreSqlFixture _postgres;

    public PersistenceTransactionTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_TwoSchemas_ReadsOwnWritesAndCommitsAtomically()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        var inventory = owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        IDataAccess<CharacterEntity>? escaped = null;
        await owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async tx =>
            {
                escaped = tx.GetDataAccess<CharacterEntity>();
                await escaped.UpsertAsync(new CharacterEntity { Id = new Serial(1), Name = "committed" });
                await tx.GetDataAccess<InventoryEntity>()
                    .UpsertAsync(new InventoryEntity { Id = new Serial(2), Balance = 5 });
                Assert.Equal("committed", (await escaped.GetByIdAsync(new Serial(1)))!.Name);
                Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM plugin_characters.characters"));
            }
        );
        Assert.Single(await store.GetAllAsync());
        Assert.Single(await inventory.GetAllAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => escaped!.GetAllAsync());
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("constraint")]
    [InlineData("caught_constraint")]
    [InlineData("standalone")]
    [InlineData("nested")]
    [InlineData("wrong_target")]
    [InlineData("cancel")]
    public async Task ExecuteInTransactionAsync_FailureOrCaughtMisuse_RollsBackWholeGroup(string failure)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        var inventory = owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<Exception>(() => owner.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Realm,
                async tx =>
                {
                    await tx.GetDataAccess<CharacterEntity>()
                        .UpsertAsync(new CharacterEntity { Id = new Serial(1), Name = "rollback" });
                    await tx.GetDataAccess<InventoryEntity>()
                        .UpsertAsync(new InventoryEntity { Id = new Serial(2), Balance = 5 });
                    if (failure == "callback")
                    {
                        throw new InvalidOperationException("original");
                    }

                    if (failure is "constraint" or "caught_constraint")
                    {
                        try
                        {
                            await tx.GetDataAccess<CharacterEntity>()
                                .UpsertAsync(new CharacterEntity { Id = new Serial(3), Name = new string('x', 200) });
                        }
                        catch when (failure == "caught_constraint")
                        {
                        }
                    }

                    if (failure == "standalone")
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAllAsync());
                    }

                    if (failure == "nested")
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(() =>
                            owner.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Realm, _ => Task.CompletedTask)
                        );
                    }

                    if (failure == "wrong_target")
                    {
                        Assert.Throws<InvalidOperationException>(() => tx.GetDataAccess<AccountsSharedEntity>());
                    }

                    if (failure == "cancel")
                    {
                        cancellation.Cancel();
                    }
                },
                cancellation.Token
            )
        );
        Assert.Empty(await store.GetAllAsync());
        Assert.Empty(await inventory.GetAllAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteInTransactionAsync_ConcurrentOrUnawaitedRead_DrainsAndPoisons(bool unawaited)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var held = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
                         "LOCK plugin_characters.characters IN ACCESS EXCLUSIVE MODE",
                         blocker,
                         held
                     ))
        {
            await command.ExecuteNonQueryAsync();
        }

        var callbackFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? pending = null;
        var transaction = owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async tx =>
            {
                var scoped = tx.GetDataAccess<CharacterEntity>();
                await tx.GetDataAccess<InventoryEntity>().UpsertAsync(new InventoryEntity { Id = new Serial(1) });
                pending = scoped.GetAllAsync();
                await DatabaseBarrier.WaitForBlockedReadAsync(database);
                if (!unawaited)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => scoped.GetAllAsync());
                }

                callbackFinished.SetResult();
                if (!unawaited)
                {
                    await pending;
                }
            }
        );
        try
        {
            await callbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(transaction.IsCompleted);
        }
        finally
        {
            await held.RollbackAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction);
        if (pending is not null)
        {
            await pending;
        }

        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM plugin_inventory.inventories"));
        Assert.Empty(await store.GetAllAsync());
    }

    [Theory]
    [InlineData("dispose")]
    [InlineData("initialize")]
    [InlineData("preview")]
    [InlineData("sync")]
    [InlineData("save")]
    public async Task ExecuteInTransactionAsync_CaughtLifecycleReentry_PoisonsWithoutDeadlock(string operation)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Realm,
                async tx =>
                {
                    await tx.GetDataAccess<CharacterEntity>().UpsertAsync(new CharacterEntity { Id = new Serial(1) });
                    await Assert.ThrowsAsync<InvalidOperationException>(() => operation switch
                        {
                            "dispose"    => owner.DisposeAsync().AsTask(),
                            "initialize" => owner.InitializeAsync(),
                            "preview"    => owner.PreviewSchemaAsync(),
                            "sync"       => owner.SynchronizeSchemaAsync(),
                            _            => owner.SaveAllAsync()
                        }
                    );
                }
            )
            .WaitAsync(TimeSpan.FromSeconds(10))
        );
        Assert.Empty(await store.GetAllAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteInTransactionAsync_DifferentTarget_RejectionPoisonsButIndependentOperationsProceed(
        bool standalone
    )
    {
        await using var accounts = await _postgres.CreateDatabaseAsync();
        await using var realm = await _postgres.CreateDatabaseAsync();
        await using var owner = new MoongatePersistenceService(
            new PostgreSqlPersistenceOptions(
                [
                    new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Accounts, accounts.ConnectionString),
                    new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, realm.ConnectionString)
                ],
                true
            )
        );
        owner.RegisterModule(
            new TestPersistenceModule(
                "accounts",
                "plugin_shared",
                PersistenceDatabaseTarget.Accounts,
                [typeof(AccountsSharedEntity)]
            )
        );
        owner.RegisterModule(
            new TestPersistenceModule("realm", "plugin_shared", PersistenceDatabaseTarget.Realm, [typeof(RealmSharedEntity)])
        );
        var accountStore = owner.RegisterEntity<AccountsSharedEntity>();
        var realmStore = owner.RegisterEntity<RealmSharedEntity>();
        await owner.InitializeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async tx =>
            {
                await tx.GetDataAccess<RealmSharedEntity>().UpsertAsync(new RealmSharedEntity { Id = new Serial(1) });
                entered.SetResult();
                await release.Task;
                if (standalone)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => accountStore.GetAllAsync());
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => tx.GetDataAccess<AccountsSharedEntity>());
                }
            }
        );
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await accountStore.UpsertAsync(new AccountsSharedEntity { Id = new Serial(1) })
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Single(await accountStore.GetAllAsync());
            Assert.Empty(await realmStore.GetAllAsync());
        }
        finally
        {
            release.TrySetResult();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Empty(await realmStore.GetAllAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ClosingOwnerStillPoisonsCaughtStandaloneReentry()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transaction = owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async tx =>
            {
                await tx.GetDataAccess<CharacterEntity>().UpsertAsync(new CharacterEntity { Id = new Serial(1) });
                entered.SetResult();
                await release.Task;
                await Assert.ThrowsAnyAsync<Exception>(() => store.GetAllAsync());
            }
        );
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var close = owner.DisposeAsync().AsTask();
        release.SetResult();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction);
        }
        finally
        {
            await close;
        }

        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM plugin_characters.characters"));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollbackConnectionFails_PreservesOriginalCallbackErrorWithoutRetry()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var original = new InvalidOperationException("original callback error");
        var calls = 0;
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Realm,
                async tx =>
                {
                    calls++;
                    await tx.GetDataAccess<CharacterEntity>().UpsertAsync(new CharacterEntity { Id = new Serial(1) });
                    Assert.True(
                        await database.ScalarAsync<bool>(
                            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = current_database() AND state = 'idle in transaction' AND pid <> pg_backend_pid()"
                        )
                    );
                    throw original;
                }
            )
        );
        Assert.Same(original, observed);
        Assert.Equal(1, calls);
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_OuterCancellation_CancelsScopedIoWithoutAnExplicitChildToken()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>();
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var held = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
                         "LOCK plugin_characters.characters IN ACCESS EXCLUSIVE MODE",
                         blocker,
                         held
                     ))
        {
            await command.ExecuteNonQueryAsync();
        }

        using var cancellation = new CancellationTokenSource();
        var pending = owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async tx =>
            {
                await tx.GetDataAccess<InventoryEntity>().UpsertAsync(new InventoryEntity { Id = new Serial(1) });
                await tx.GetDataAccess<CharacterEntity>().GetAllAsync();
            },
            cancellation.Token
        );
        try
        {
            await DatabaseBarrier.WaitForBlockedReadAsync(database);
            cancellation.Cancel();
            var error = await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(
                error is OperationCanceledException || error.InnerException is OperationCanceledException,
                $"Expected cancellation, received {error.GetType().Name}."
            );
        }
        finally
        {
            await held.RollbackAsync();
            await pending.ConfigureAwait(
                ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
            );
        }

        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM plugin_inventory.inventories"));
        Assert.Empty(await store.GetAllAsync());
    }
}
