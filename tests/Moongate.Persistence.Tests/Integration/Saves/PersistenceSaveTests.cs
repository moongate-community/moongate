using Moongate.Core.Primitives;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Persistence.Tests.TestSupport.Persistence;

namespace Moongate.Persistence.Tests.Integration.Saves;

[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceSaveTests
{
    private readonly PostgreSqlFixture _postgres;
    public PersistenceSaveTests(PostgreSqlFixture postgres) { _postgres = postgres; }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("null")]
    [InlineData("same")]
    [InlineData("changed_id")]
    [InlineData("no_capture")]
    [InlineData("twice")]
    [InlineData("caught_reentry")]
    public async Task SaveAllAsync_InvalidCapture_WritesNothingForTarget(string failure)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var live = new CharacterEntity { Id = new Serial(1), Name = "live" };
        var store = owner.RegisterEntity<CharacterEntity>(() => failure == "duplicate" ? [live, live] : [live], e => failure switch
        {
            "zero" => new CharacterEntity(),
            "null" => null!,
            "same" => e,
            "changed_id" => new CharacterEntity { Id = new Serial(2) },
            _ => new CharacterEntity { Id = e.Id, Name = e.Name }
        });
        var inventory = owner.RegisterEntity<InventoryEntity>(() => [new InventoryEntity { Id = new Serial(5) }], e => new InventoryEntity { Id = e.Id });
        await owner.InitializeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => owner.SaveAllAsync(async (capture, _) =>
        {
            if (failure == "no_capture")
            {
                return;
            }

            capture();
            if (failure == "twice") { try { capture(); } catch (InvalidOperationException) { } }
            if (failure == "caught_reentry")
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAllAsync());
            }
        }));
        Assert.Empty(await store.GetAllAsync());
        Assert.Empty(await inventory.GetAllAsync());
    }

    [Fact]
    public async Task SaveAllAsync_CaptureThenDelete_OrdersMutationsWithoutResurrection()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var live = new CharacterEntity { Id = new Serial(1), Name = "snapshot" };
        var store = owner.RegisterEntity<CharacterEntity>(() => [live], e => new CharacterEntity { Id = e.Id, Name = e.Name });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = owner.SaveAllAsync(async (capture, _) => { capture(); captured.SetResult(); await release.Task; });
        await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
        live.Name = "later";
        var deletion = store.DeleteAsync(live.Id);
        Assert.False(deletion.IsCompleted);
        release.SetResult();
        await save;
        Assert.True(await deletion);
        Assert.Null(await store.GetByIdAsync(live.Id));
    }

    [Fact]
    public async Task SaveAllAsync_CaptureOnIndependentContext_RejectsCaughtSourceReentry()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>(() =>
        {
            try { owner.PreviewSchemaAsync().GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
            return [new CharacterEntity { Id = new Serial(1) }];
        }, e => new CharacterEntity { Id = e.Id });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync((capture, _) =>
        {
            using (ExecutionContext.SuppressFlow()) { return Task.Run(capture, CancellationToken.None); }
        }));
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task SaveAllAsync_SnapshotMutatesLiveIdentity_RejectsChangedIdentity()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var live = new CharacterEntity { Id = new Serial(1) };
        var store = owner.RegisterEntity<CharacterEntity>(() => [live], e =>
        {
            e.Id = new Serial(2);
            return new CharacterEntity { Id = e.Id };
        });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task DisposeAsync_PendingCapture_DrainsAdmittedSaveAndRejectsNewWork()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>(() => [new CharacterEntity { Id = new Serial(1) }], e => new CharacterEntity { Id = e.Id });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = owner.SaveAllAsync(async (capture, _) => { capture(); captured.SetResult(); await release.Task; });
        await captured.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var close = owner.DisposeAsync().AsTask();
        try
        {
            Assert.False(close.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => store.GetAllAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.SaveAllAsync());
        }
        finally { release.TrySetResult(); }
        await save;
        await close;
        await owner.DisposeAsync();
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM plugin_characters.characters"));
    }

    [Fact]
    public async Task SaveAllAsync_DeferredSecondInvocation_RejectsWithoutCallingSourcesAgain()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var count = 0;
        owner.RegisterEntity<CharacterEntity>(() => { count++; return [new CharacterEntity { Id = new Serial(1) }]; }, e => new CharacterEntity { Id = e.Id });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        Action? deferred = null;
        await owner.SaveAllAsync((capture, _) => { deferred = capture; capture(); return Task.CompletedTask; });
        Assert.Throws<InvalidOperationException>(() => deferred!());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAllAsync_NestedSnapshotAndAbsentSource_PreservesDetachedValuesAndExistingRows()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = new MoongatePersistenceService(new PostgreSqlPersistenceOptions(
            [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, database.ConnectionString)], true));
        owner.RegisterModule(new TestPersistenceModule("snapshot", "plugin_snapshot", PersistenceDatabaseTarget.Realm, [typeof(SnapshotEntity)]));
        var live = new SnapshotEntity { Id = new Serial(1), Payload = [10, 20] };
        var present = true;
        var store = owner.RegisterEntity<SnapshotEntity>(() => present ? [live] : [], e => new SnapshotEntity { Id = e.Id, Payload = e.Payload.ToArray() });
        await owner.InitializeAsync();
        await owner.SaveAllAsync((capture, _) => { capture(); live.Payload[0] = 99; return Task.CompletedTask; });
        Assert.Equal(new byte[] { 10, 20 }, (await store.GetByIdAsync(live.Id))!.Payload);
        present = false;
        await owner.SaveAllAsync();
        Assert.Single(await store.GetAllAsync());
    }

    [Fact]
    public async Task SaveAllAsync_LaterTargetCaptureFails_KeepsEarlierTargetCommit()
    {
        using var logs = new PersistenceLogCapture();
        await using var accounts = await _postgres.CreateDatabaseAsync();
        await using var realm = await _postgres.CreateDatabaseAsync();
        await using var owner = new MoongatePersistenceService(new PostgreSqlPersistenceOptions(
            [new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Accounts, accounts.ConnectionString),
             new PersistenceDatabaseOptions(PersistenceDatabaseTarget.Realm, realm.ConnectionString)], true));
        owner.RegisterModule(new TestPersistenceModule("accounts", "plugin_shared", PersistenceDatabaseTarget.Accounts, [typeof(AccountsSharedEntity)]));
        owner.RegisterModule(new TestPersistenceModule("realm", "plugin_shared", PersistenceDatabaseTarget.Realm, [typeof(RealmSharedEntity)]));
        var accountStore = owner.RegisterEntity<AccountsSharedEntity>(() => [new AccountsSharedEntity { Id = new Serial(1) }], e => new AccountsSharedEntity { Id = e.Id });
        var realmStore = owner.RegisterEntity<RealmSharedEntity>(() => [new RealmSharedEntity { Id = new Serial(1) }], e => e);
        await owner.InitializeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());
        Assert.Single(await accountStore.GetAllAsync());
        Assert.Empty(await realmStore.GetAllAsync());
        var committed = Assert.Single(logs.Events, e => e.MessageTemplate.Text.StartsWith("PostgreSQL snapshot committed", StringComparison.Ordinal));
        Assert.Equal("Accounts", committed.Properties["Target"].ToString());
        Assert.Equal("1", committed.Properties["EntityCount"].ToString());
        Assert.DoesNotContain(logs.Events, e => e.MessageTemplate.Text.StartsWith("PostgreSQL world save completed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAllAsync_LaterSourceFailureOrCancellation_RollsBackWholeTarget(bool cancel)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var store = owner.RegisterEntity<CharacterEntity>(() => [new CharacterEntity { Id = new Serial(1) }], e => new CharacterEntity { Id = e.Id });
        owner.RegisterEntity<InventoryEntity>(() => [new InventoryEntity { Id = new Serial(2) }], e => cancel ? new InventoryEntity { Id = e.Id } : e);
        await owner.InitializeAsync();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<Exception>(() => owner.SaveAllAsync((capture, _) =>
        {
            capture();
            cancellation.Cancel();
            return Task.CompletedTask;
        }, cancellation.Token));
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task SaveAllAsync_LateSqlFailure_DoesNotDurablyCommitEarlierBatches()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var values = Enumerable.Range(1, 260).Select(i => new CharacterEntity { Id = new Serial((uint)i), Name = i == 260 ? new string('x', 200) : "valid" }).ToArray();
        var store = owner.RegisterEntity<CharacterEntity>(() => values, e => new CharacterEntity { Id = e.Id, Name = e.Name });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => owner.SaveAllAsync());
        Assert.Empty(await store.GetAllAsync());
        values[259].Name = "fixed";
        await owner.SaveAllAsync();
        Assert.Equal(260, (await store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task SaveAllAsync_LaterCaptureInvalid_PerformsNoSqlWritesEvenBeforeRollback()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        owner.RegisterEntity<CharacterEntity>(() => [new CharacterEntity { Id = new Serial(1) }], e => new CharacterEntity { Id = e.Id });
        owner.RegisterEntity<InventoryEntity>(() => [new InventoryEntity { Id = new Serial(2) }], e => e);
        await owner.InitializeAsync();
        await database.ExecuteAsync("""
            CREATE SEQUENCE plugin_characters.write_attempts;
            CREATE FUNCTION plugin_characters.track_write() RETURNS trigger LANGUAGE plpgsql AS
            'BEGIN PERFORM nextval(''plugin_characters.write_attempts''); RETURN NEW; END';
            CREATE TRIGGER track_write BEFORE INSERT ON plugin_characters.characters FOR EACH ROW EXECUTE FUNCTION plugin_characters.track_write();
            """);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());
        Assert.False(await database.ScalarAsync<bool>("SELECT is_called FROM plugin_characters.write_attempts"));
    }

    [Fact]
    public async Task SaveAllAsync_ExplicitTransactionAlreadyAdmitted_WaitsBeforeEnumeratingSource()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var captures = 0;
        var live = new CharacterEntity { Id = new Serial(1), Name = "old" };
        var store = owner.RegisterEntity<CharacterEntity>(() => { captures++; return [live]; }, e => new CharacterEntity { Id = e.Id, Name = e.Name });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transaction = owner.ExecuteInTransactionAsync(PersistenceDatabaseTarget.Realm, async tx =>
        {
            await tx.GetDataAccess<CharacterEntity>().UpsertAsync(new CharacterEntity { Id = live.Id, Name = "new" });
            entered.SetResult();
            await release.Task;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var save = owner.SaveAllAsync();
        try
        {
            Assert.False(save.IsCompleted);
            Assert.Equal(0, captures);
            live.Name = "new";
        }
        finally { release.SetResult(); }
        await transaction;
        await save;
        Assert.Equal("new", (await store.GetByIdAsync(live.Id))!.Name);
        Assert.Equal(1, captures);
    }

    [Theory]
    [InlineData("success", false)]
    [InlineData("cancellation", false)]
    [InlineData("failure", true)]
    public async Task SaveAllAsync_DispatcherExitsDuringActiveCapture_DrainsBeforeMutationAndDisposal(string outcome, bool blockSnapshot)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        using var releaseCapture = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var captureStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Inline completion on a pool thread makes the save's dispatcher-exit path run before SetResult returns.
        var dispatcher = new TaskCompletionSource();
        var live = new CharacterEntity { Id = new Serial(1), Name = "must not be saved" };
        var store = owner.RegisterEntity<CharacterEntity>(() =>
        {
            if (!blockSnapshot)
            {
                captureStarted.SetResult();
                releaseCapture.Wait();
            }
            return [live];
        }, entity =>
        {
            if (blockSnapshot)
            {
                captureStarted.SetResult();
                releaseCapture.Wait();
                throw new InvalidOperationException("late snapshot failure");
            }
            return new CharacterEntity { Id = entity.Id, Name = entity.Name };
        });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        Task? captureTask = null;
        var save = owner.SaveAllAsync((capture, _) =>
        {
            using (ExecutionContext.SuppressFlow())
            {
                captureTask = Task.Run(capture, CancellationToken.None);
            }
            return dispatcher.Task;
        }, cancellation.Token);
        Task? mutation = null;
        Task? close = null;
        Exception? original = outcome switch
        {
            "cancellation" => new OperationCanceledException("dispatcher cancelled", cancellation.Token),
            "failure" => new ApplicationException("dispatcher failed"),
            _ => null
        };
        try
        {
            await captureStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            mutation = store.UpsertAsync(new CharacterEntity { Id = new Serial(2), Name = "independent" });
            close = owner.DisposeAsync().AsTask();
            await Task.Run(() =>
            {
                if (outcome == "cancellation")
                {
                    cancellation.Cancel();
                }
                if (original is null)
                {
                    dispatcher.SetResult();
                }
                else
                {
                    dispatcher.SetException(original);
                }
            }, CancellationToken.None);
            Assert.False(save.IsCompleted);
            Assert.False(mutation.IsCompleted);
            Assert.False(close.IsCompleted);
        }
        finally
        {
            releaseCapture.Set();
            dispatcher.TrySetResult();
            if (captureTask is not null)
            {
                await captureTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            }
            if (mutation is not null)
            {
                await mutation;
            }
            if (close is not null)
            {
                await close;
            }
        }
        var failure = await Record.ExceptionAsync(() => save);
        if (original is null)
        {
            Assert.IsType<InvalidOperationException>(failure);
        }
        else
        {
            Assert.Same(original, failure);
        }
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT count(*) FROM plugin_characters.characters WHERE id = 1"));
        Assert.Equal("independent", await database.ScalarAsync<string>("SELECT name FROM plugin_characters.characters WHERE id = 2"));
    }

    [Fact]
    public async Task SaveAllAsync_DispatcherNeverStartsCapture_ClosesWithoutWaitingAndRejectsDeferredAction()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = FacadeFixture.Create(database);
        var calls = 0;
        var store = owner.RegisterEntity<CharacterEntity>(() =>
        {
            calls++;
            return [new CharacterEntity { Id = new Serial(1) }];
        }, entity => new CharacterEntity { Id = entity.Id });
        owner.RegisterEntity<InventoryEntity>();
        await owner.InitializeAsync();
        Action? deferred = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync((capture, _) =>
        {
            deferred = capture;
            return Task.CompletedTask;
        }).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Throws<InvalidOperationException>(() => deferred!());
        Assert.Equal(0, calls);
        Assert.Empty(await store.GetAllAsync());
    }
}
