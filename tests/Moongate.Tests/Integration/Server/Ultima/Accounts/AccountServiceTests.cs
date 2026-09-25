using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Server.Ultima;
using Npgsql;

namespace Moongate.Tests.Integration.Server.Ultima.Accounts;

[Collection(PostgresTestCollection.Name)]
public sealed class AccountServiceTests
{
    [Fact]
    public async Task ListAccountsPageAsync_KeysetPagination_PreservesOrderAndPrivileges()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();

        for (var i = 0; i < 4; i++)
        {
            var result = await fixture.Service.CreateAccountAsync(
                new()
                {
                    Username = $"user{i}", Password = fixture.Password,
                    AccountType = AccountType.Administrator, CanAccessApi = i == 0
                }
            );
            Assert.True(result.Success, result.Exception?.ToString());
        }

        var first = await fixture.Service.ListAccountsPageAsync(Serial.Zero, 2);
        var second = await fixture.Service.ListAccountsPageAsync(first.NextAfterId, 2);
        Assert.Equal(new[] { "user0", "user1" }, first.Items.Select(a => a.Username));
        Assert.Equal(new[] { "user2", "user3" }, second.Items.Select(a => a.Username));
        Assert.Equal(first.Items[1].Id, first.NextAfterId);
        Assert.Equal(Serial.Zero, second.NextAfterId);
        Assert.True(first.Items[0].CanAccessApi);
        Assert.False(first.Items[1].CanAccessApi);
        Assert.All(first.Items, a => Assert.Equal(AccountType.Administrator, a.AccountType));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.ListAccountsPageAsync(Serial.Zero, 201));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.ListAccountsPageAsync(Serial.Zero, 0));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task LoginAsync_ConcurrentSecurityChange_DoesNotRestoreStalePrivileges(bool locked)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var account = await fixture.SeedAsync();
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE auth.accounts SET account_type=0, is_locked=@locked WHERE id=42",
            connection,
            transaction
        );
        command.Parameters.AddWithValue("locked", locked);
        await command.ExecuteNonQueryAsync();
        var login = fixture.Service.LoginAsync("alice", fixture.Password);

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!await fixture.Database.ScalarAsync<bool>(
                       "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock')"
                   ))
            {
                await Task.Delay(10, deadline.Token);
            }

            await transaction.CommitAsync();
            var result = await login.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(locked, result is null);
            var stored = (await fixture.Accounts.GetByIdAsync(account.Id))!;
            Assert.Equal(AccountType.Regular, stored.AccountType);
            Assert.Equal(locked, stored.IsLocked);
        }
        finally
        {
            await transaction.DisposeAsync();
            await ((Task)login).ConfigureAwait(
                ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
            );
        }
    }

    [Fact]
    public async Task ListAccountsAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        Assert.Empty(await fixture.Service.ListAccountsAsync());
    }

    [Fact]
    public async Task ListAccountsAsync_ReturnsAllAccountsIncludingLockedAsDetachedEntities()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var locked = await fixture.SeedAsync(true);
        var created = await fixture.Service.CreateAccountAsync("bob", fixture.Password);
        Assert.True(created.Success, created.Exception?.ToString());

        var accounts = (await fixture.Service.ListAccountsAsync()).ToArray();
        Assert.Equal(new[] { "alice", "bob" }, accounts.Select(account => account.Username).Order().ToArray());
        var listedLocked = Assert.Single(accounts, account => account.Id == locked.Id);
        Assert.True(listedLocked.IsLocked);
        Assert.Equal(locked.AccountType, listedLocked.AccountType);
        Assert.Contains(accounts, account => account.Id == created.Account!.Id && !account.IsLocked);

        listedLocked.Username = "modified only in memory";
        Assert.Equal("alice", (await fixture.Accounts.GetByIdAsync(locked.Id))!.Username);
    }

    [Fact]
    public async Task ListAccountsAsync_Canceled_PropagatesCallerToken()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                fixture.Service.ListAccountsAsync(cancellation.Token)
            );
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Theory, InlineData("login"), InlineData("create"), InlineData("list")]
    public async Task Request_CanceledWhileWaitingForDatabase_PropagatesCancellation(string operation)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        await using var blocker = new NpgsqlConnection(fixture.Database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "LOCK TABLE auth.accounts IN ACCESS EXCLUSIVE MODE",
            blocker,
            transaction
        );
        await command.ExecuteNonQueryAsync();
        using var cancellation = new CancellationTokenSource();
        Task request = operation switch
        {
            "login" => fixture.Service.LoginAsync("alice", fixture.Password, cancellation.Token),
            "list"  => fixture.Service.ListAccountsAsync(cancellation.Token),
            _       => fixture.Service.CreateAccountAsync("alice", fixture.Password, cancellationToken: cancellation.Token)
        };

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!await fixture.Database.ScalarAsync<bool>(
                       "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE relation = 'auth.accounts'::regclass AND NOT granted)"
                   ))
            {
                await Task.Delay(10, deadline.Token);
            }

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            cancellation.Cancel();
            await transaction.RollbackAsync();
            await request.ConfigureAwait(
                ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
            );
        }

        Assert.Empty(await fixture.Accounts.GetAllAsync());
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task CreateAccountAsync_IndependentServices_ReserveUniqueIdsAndRejectDuplicateUsernames(bool sameUsername)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        using var peerDirectory = new TemporaryPersistenceDirectory();
        using var peer = new Container();
        peer.RegisterInstance(new DirectoriesConfig(peerDirectory.Path, []));
        peer.RegisterMoongatePersistence(
            new([new(PersistenceDatabaseTarget.Accounts, fixture.Database.ConnectionString)], true)
        );
        new MoongateUltimaPlugin().Register(peer);
        await using var persistence = peer.Resolve<MoongatePersistenceService>();
        await persistence.InitializeAsync();
        var peerService = peer.Resolve<IAccountService>();
        var results = await Task.WhenAll(
            Enumerable.Range(0, 12)
                .Select(index =>
                    (index % 2 == 0 ? fixture.Service : peerService).CreateAccountAsync(
                        sameUsername ? "alice" : $"user{index}",
                        fixture.Password
                    )
                )
        );
        var successes = results.Where(result => result.Success).ToArray();
        Assert.Equal(sameUsername ? 1 : 12, successes.Length);
        Assert.All(
            results.Where(result => !result.Success),
            result =>
                Assert.Equal(AccountCreateResultType.UsernameAlreadyExists, result.ResultType)
        );
        Assert.Equal(successes.Length, successes.Select(result => result.Account!.Id).Distinct().Count());
        Assert.Equal(successes.Length, (await fixture.Accounts.GetAllAsync()).Count);
    }

    [Fact]
    public async Task CreateAccountAsync_Restart_ContinuesAfterPersistedIdentity()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var first = await fixture.Service.CreateAccountAsync("alice", fixture.Password);
        Assert.True(first.Success, first.Exception?.ToString());
        using var peerDirectory = new TemporaryPersistenceDirectory();
        using var peer = new Container();
        peer.RegisterInstance(new DirectoriesConfig(peerDirectory.Path, []));
        peer.RegisterMoongatePersistence(
            new([new(PersistenceDatabaseTarget.Accounts, fixture.Database.ConnectionString)], true)
        );
        new MoongateUltimaPlugin().Register(peer);
        await using var persistence = peer.Resolve<MoongatePersistenceService>();
        await persistence.InitializeAsync();
        var second = await peer.Resolve<IAccountService>().CreateAccountAsync("bob", fixture.Password);
        Assert.True(second.Success, second.Exception?.ToString());
        Assert.True(second.Account!.Id.Value > first.Account!.Id.Value);
        Assert.Equal(2, (await fixture.Accounts.GetAllAsync()).Count);
    }

    [Theory, InlineData(AccountType.Regular), InlineData(AccountType.GameMaster), InlineData(AccountType.Administrator)]
    public async Task CreateAccountAsync_ValidAccount_PersistsHashAndRequestedType(AccountType accountType)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var before = DateTime.UtcNow;
        var result = await fixture.Service.CreateAccountAsync("alice", fixture.Password, accountType);
        Assert.True(result.Success, result.Exception?.ToString());
        Assert.Equal(AccountCreateResultType.Success, result.ResultType);
        Assert.Null(result.Exception);
        Assert.NotNull(result.Account);
        Assert.NotEqual(default, result.Account.Id);
        var stored = await fixture.Accounts.GetByIdAsync(result.Account.Id);
        Assert.NotNull(stored);
        Assert.Equal("alice", stored.Username);
        Assert.Equal(accountType, stored.AccountType);
        Assert.NotEqual(fixture.Password, stored.HashPassword);
        Assert.True(HashUtils.VerifyPassword(fixture.Password, stored.HashPassword));
        Assert.False(stored.IsLocked);
        Assert.Null(stored.LastLoginAt);
        Assert.InRange(stored.CreatedAt, before.AddSeconds(-1), DateTime.UtcNow);
        Assert.InRange(stored.UpdatedAt, before.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateAccountAsync_TwoUsers_PersistsDistinctIdentities()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var first = await fixture.Service.CreateAccountAsync("alice", fixture.Password);
        var second = await fixture.Service.CreateAccountAsync("bob", fixture.Password);
        Assert.True(first.Success, first.Exception?.ToString());
        Assert.True(second.Success, second.Exception?.ToString());
        Assert.NotEqual(first.Account!.Id, second.Account!.Id);
        Assert.Equal(2, (await fixture.Accounts.GetAllAsync()).Count);
    }

    [Fact]
    public async Task CreateAccountAsync_ExistingUsername_DoesNotReplaceAccount()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var original = await fixture.SeedAsync();
        var result = await fixture.Service.CreateAccountAsync(
            "alice",
            Guid.NewGuid().ToString("N"),
            AccountType.Administrator
        );
        Assert.False(result.Success);
        Assert.Equal(AccountCreateResultType.UsernameAlreadyExists, result.ResultType);
        Assert.Null(result.Account);
        Assert.Null(result.Exception);
        var stored = Assert.Single(await fixture.Accounts.GetAllAsync());
        Assert.Equal(original.Id, stored.Id);
        Assert.Equal(original.HashPassword, stored.HashPassword);
        Assert.Equal(AccountType.GameMaster, stored.AccountType);
    }

    [Theory, InlineData(""), InlineData("   ")]
    public async Task CreateAccountAsync_EmptyPassword_DoesNotPersistAccount(string password)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var result = await fixture.Service.CreateAccountAsync("alice", password);
        Assert.False(result.Success);
        Assert.Null(result.Account);
        Assert.Empty(await fixture.Accounts.GetAllAsync());
    }

    [Fact]
    public async Task CreateAccountAsync_Canceled_PropagatesCancellationWithoutPersisting()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.CreateAccountAsync(
                "alice",
                fixture.Password,
                cancellationToken: cancellation.Token
            )
        );
        Assert.Empty(await fixture.Accounts.GetAllAsync());
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_PersistsLastLoginWithoutChangingIdentityOrPrivileges()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var original = await fixture.SeedAsync();
        var before = DateTime.UtcNow;
        var result = await fixture.Service.LoginAsync("alice", fixture.Password);
        Assert.NotNull(result);
        Assert.Equal(original.Id, result.Id);
        Assert.NotNull(result.LastLoginAt);
        var stored = await fixture.Accounts.GetByIdAsync(original.Id);
        Assert.NotNull(stored);
        Assert.NotNull(stored.LastLoginAt);
        Assert.InRange(stored.LastLoginAt.Value, before.AddSeconds(-1), DateTime.UtcNow);
        Assert.Equal(original.HashPassword, stored.HashPassword);
        Assert.Equal(original.AccountType, stored.AccountType);
        Assert.Equal(original.CreatedAt, stored.CreatedAt);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task LoginAsync_UnknownUserOrWrongPassword_DoesNotUpdateLastLogin(bool unknownUser)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var original = await fixture.SeedAsync();
        var result = await fixture.Service.LoginAsync(
            unknownUser ? "missing" : "alice",
            unknownUser ? fixture.Password : Guid.NewGuid().ToString("N")
        );
        Assert.Null(result);
        var stored = await fixture.Accounts.GetByIdAsync(original.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.LastLoginAt);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_RejectsCorrectPasswordWithoutUpdatingLastLogin()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var original = await fixture.SeedAsync(true);
        var result = await fixture.Service.LoginAsync("alice", fixture.Password);
        Assert.Null(result);
        var stored = await fixture.Accounts.GetByIdAsync(original.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.LastLoginAt);
    }

    [Fact]
    public async Task LoginAsync_Canceled_PropagatesCancellation()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.LoginAsync(
                "alice",
                fixture.Password,
                cancellation.Token
            )
        );
    }
}
