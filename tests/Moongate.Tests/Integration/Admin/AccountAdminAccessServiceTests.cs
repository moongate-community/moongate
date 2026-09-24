using System.Security.Cryptography;
using System.Text;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Exceptions.Admin;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Admin;

[Collection(PostgresTestCollection.Name)]
public sealed class AccountAdminAccessServiceTests
{
    [Theory, InlineData(CommandSourceType.Console, true), InlineData(CommandSourceType.InGame, false)]
    public async Task AccountCommand_ApiAccess_RequiresLocalConsole(CommandSourceType source, bool allowed)
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        var account = (await fixture.Accounts.Service.CreateAccountAsync(
                           "admin",
                           fixture.Accounts.Password,
                           AccountType.Administrator
                       )).Account!;
        var command = new AccountCommand(fixture.Accounts.Service, fixture.Authority);
        var context = new CommandContext(
            "account api-access admin on",
            "account",
            ["api-access", "admin", "on"],
            source,
            null
        );
        await command.ExecuteAsync(context);
        Assert.Equal(allowed, (await fixture.Accounts.Accounts.GetByIdAsync(account.Id))!.CanAccessApi);
        Assert.Equal(
            allowed ? CommandOutputLevel.Information : CommandOutputLevel.Error,
            Assert.Single(context.Output).Level
        );
    }

    [Theory, InlineData("unknown"), InlineData("wrong-password"), InlineData("locked"), InlineData("unknown-role")]
    public async Task LoginAsync_InvalidAccountState_DoesNotIssueOrRecordLogin(string state)
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        var result = await fixture.Accounts.Service.CreateAccountAsync(
                         new()
                         {
                             Username = "admin", Password = fixture.Accounts.Password, CanAccessApi = true
                         }
                     );
        var account = result.Account!;
        account.IsLocked = state == "locked";

        if (state == "unknown-role") { account.AccountType = (AccountType)99; }
        await fixture.Accounts.Accounts.UpsertAsync(account);
        Assert.Null(
            await fixture.Authority.LoginAsync(
                state == "unknown" ? "missing" : "admin",
                state == "wrong-password" ? Guid.NewGuid().ToString("N") : fixture.Accounts.Password
            )
        );
        Assert.Equal(0, fixture.Store.IssuedCount);
        Assert.Null((await fixture.Accounts.Accounts.GetByIdAsync(account.Id))!.LastLoginAt);
    }

    [Fact]
    public async Task SetApiAccessAsync_ProvisionAndDisable_RevokesSessionsAndPreservesRole()
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        var result = await fixture.Accounts.Service.CreateAccountAsync(
                         "admin",
                         fixture.Accounts.Password,
                         AccountType.Administrator
                     );
        await fixture.Authority.SetApiAccessAsync("admin", true);
        Assert.NotNull(await fixture.Authority.LoginAsync("admin", fixture.Accounts.Password));
        var digest = fixture.Store.LastDigest!;
        await fixture.PeerAuthority.SetApiAccessAsync("admin", false);
        Assert.Null(await fixture.Redis.Store.FindAsync(digest));
        Assert.Null(await fixture.Authority.LoginAsync("admin", fixture.Accounts.Password));
        Assert.Equal(
            AccountType.Administrator,
            (await fixture.Accounts.Accounts.GetByIdAsync(result.Account!.Id))!.AccountType
        );
        await fixture.Authority.SetApiAccessAsync("admin", false);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Authority.SetApiAccessAsync("missing", true));
    }

    [Fact]
    public async Task LoginAsync_DisabledApiAccess_DoesNotIssueSession()
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        await fixture.Accounts.Service.CreateAccountAsync("disabled", fixture.Accounts.Password, AccountType.Administrator);
        Assert.Null(await fixture.Authority.LoginAsync("disabled", fixture.Accounts.Password));
        Assert.Equal(0, fixture.Store.IssuedCount);
    }

    [Theory, InlineData("password"), InlineData("role"), InlineData("locked"), InlineData("disabled"), InlineData("revoke")]
    public async Task LoginAsync_RacesSecurityMutationAcrossOwners_OldSessionIsInvalidated(string change)
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        var created = await fixture.Accounts.Service.CreateAccountAsync(
                          new()
                          {
                              Username = "admin", Password = fixture.Accounts.Password,
                              AccountType = AccountType.Administrator, CanAccessApi = true
                          }
                      );
        var account = created.Account!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeIssue = async () =>
                                    {
                                        entered.TrySetResult();
                                        await release.Task;
                                    };
        var login = fixture.Authority.LoginAsync("admin", fixture.Accounts.Password);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var newPassword = Guid.NewGuid().ToString("N");
        var mutation = change switch
        {
            "password" => fixture.PeerAuthority.ChangePasswordAsync(account.Id, newPassword),
            "revoke"   => fixture.PeerAuthority.RevokeSessionsAsync(account.Id),
            _ => fixture.PeerAuthority.UpdateAccessAsync(
                account.Id,
                new()
                {
                    AccountType = change == "role" ? AccountType.Regular : AccountType.Administrator,
                    CanAccessApi = change != "disabled", IsLocked = change == "locked"
                }
            )
        };

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!await fixture.Accounts.Database.ScalarAsync<bool>(
                        "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock')"
                    ))
            {
                await Task.Delay(10, deadline.Token);
            }
        }
        finally { release.TrySetResult(); }
        var result = await login.WaitAsync(TimeSpan.FromSeconds(10));
        await mutation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(result);
        Assert.Matches("^[0-9A-F]{64}$", result.AccessToken);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(result.AccessToken)));
        Assert.Equal(digest, fixture.Store.LastDigest);
        Assert.Null(await fixture.Redis.Store.FindAsync(digest));
        fixture.Store.BeforeIssue = null;
        var next = await fixture.Authority.LoginAsync(
                       "admin",
                       change == "password" ? newPassword : fixture.Accounts.Password
                   );

        if (change is "locked" or "disabled") { Assert.Null(next); }
        else
        {
            Assert.NotNull(next);
            Assert.Equal(change == "role" ? AccountType.Regular : AccountType.Administrator, next.Account.AccountType);
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task UpdateAccessAsync_FailsAroundCommit_RemainsBlockedUntilAuthoritativeRecovery(bool afterCommit)
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        var created = await fixture.Accounts.Service.CreateAccountAsync(
                          new()
                          {
                              Username = "admin", Password = fixture.Accounts.Password,
                              AccountType = AccountType.Administrator, CanAccessApi = true
                          }
                      );
        var login = await fixture.Authority.LoginAsync("admin", fixture.Accounts.Password);
        var digest = fixture.Store.LastDigest!;

        if (afterCommit) { fixture.Store.BeforeOpen = () => throw new AdminDependencyUnavailableException(); }
        else { fixture.Store.AfterReset = () => throw new AdminDependencyUnavailableException(); }
        await Assert.ThrowsAsync<AdminDependencyUnavailableException>(
            () => fixture.Authority.UpdateAccessAsync(
                created.Account!.Id,
                new() { AccountType = AccountType.Regular, CanAccessApi = true }
            )
        );
        Assert.NotNull(login);
        Assert.True((await fixture.Redis.Store.ReadGateAsync(created.Account!.Id))!.Blocked);
        Assert.Null(await fixture.Redis.Store.FindAsync(digest));
        fixture.Store.BeforeOpen = null;
        fixture.Store.AfterReset = null;
        var recovered = await fixture.PeerAuthority.LoginAsync("admin", fixture.Accounts.Password);
        Assert.NotNull(recovered);
        Assert.Equal(afterCommit ? AccountType.Regular : AccountType.Administrator, recovered.Account.AccountType);
        Assert.Null(await fixture.Redis.Store.FindAsync(digest));
    }

    [Fact]
    public async Task LoginAsync_CommitFailure_DoesNotLeaveIssuedSession()
    {
        await using var fixture = await AccountAdminFixture.CreateAsync();
        await fixture.Accounts.Service.CreateAccountAsync(
            new()
            {
                Username = "admin", Password = fixture.Accounts.Password, CanAccessApi = true
            }
        );
        await fixture.Accounts.Database.ExecuteAsync(
            """
            CREATE FUNCTION auth.reject_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test commit rejection'; END $$;
            CREATE CONSTRAINT TRIGGER reject_commit AFTER UPDATE ON auth.accounts DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION auth.reject_commit();
            """
        );
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Authority.LoginAsync("admin", fixture.Accounts.Password));
        Assert.NotNull(fixture.Store.LastDigest);
        Assert.Null(await fixture.Redis.Store.FindAsync(fixture.Store.LastDigest));
        Assert.Null((await fixture.Accounts.Service.ListAccountsAsync()).Single().LastLoginAt);
    }
}
