using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Moongate.Admin.Contracts.V1;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Tests.Integration.Admin;

/// <summary>
///     UpdateAccountAccess and ChangeAccountPassword through the real gRPC host, accounts database and session store.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class AdminAccountManagementGrpcTests
{
    [Fact]
    public async Task UpdateAccountAccess_LockAnotherAccount_ReturnsTheSummary_AndEndsItsSessions()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);
        var player = await CreateAsync(fixture, "player", DomainAccountType.GameMaster, true);
        var playerHeaders = await LoginAsync(fixture, "player");
        var accounts = new AdminAccounts.AdminAccountsClient(fixture.Channel);

        var summary = await accounts.UpdateAccountAccessAsync(new() { AccountId = player, IsLocked = true }, admin);

        Assert.Equal((player, "player", AccountType.GameMaster, true, true), (summary.AccountId, summary.Username, summary.AccountType, summary.CanAccessApi, summary.IsLocked));
        Assert.Equal(
            StatusCode.Unauthenticated,
            (await Assert.ThrowsAsync<RpcException>(() => new AdminServer.AdminServerClient(fixture.Channel).GetServerInfoAsync(new(), playerHeaders).ResponseAsync)).StatusCode
        );
    }

    [Fact]
    public async Task UpdateAccountAccess_OnlyTheFieldsSent_Change()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);
        var player = await CreateAsync(fixture, "player", DomainAccountType.Regular, false);

        var summary = await new AdminAccounts.AdminAccountsClient(fixture.Channel)
            .UpdateAccountAccessAsync(new() { AccountId = player, AccountType = AccountType.GameMaster }, admin);

        Assert.Equal((AccountType.GameMaster, false, false), (summary.AccountType, summary.CanAccessApi, summary.IsLocked));
    }

    [Theory,
     InlineData(0u, "nothing", StatusCode.InvalidArgument),
     InlineData(1u, "nothing", StatusCode.InvalidArgument),
     InlineData(1u, "unspecified", StatusCode.InvalidArgument),
     InlineData(4000000u, "lock", StatusCode.NotFound)]
    public async Task UpdateAccountAccess_BadRequests_AreClassified(uint account, string change, StatusCode expected)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);
        var request = new UpdateAccountAccessRequest { AccountId = account };

        if (change == "unspecified")
        {
            request.AccountType = AccountType.Unspecified;
        }
        else if (change == "lock")
        {
            request.IsLocked = true;
        }

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminAccounts.AdminAccountsClient(fixture.Channel).UpdateAccountAccessAsync(request, admin).ResponseAsync
        );

        Assert.Equal(expected, error.StatusCode);
    }

    [Theory, InlineData("lock"), InlineData("api"), InlineData("demote")]
    public async Task UpdateAccountAccess_OnYourOwnAccount_CannotCloseYouOut(string change)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);
        var self = await IdOfAsync(fixture, admin, "admin");
        var request = new UpdateAccountAccessRequest { AccountId = self };

        switch (change)
        {
            case "lock": request.IsLocked = true; break;
            case "api": request.CanAccessApi = false; break;
            default: request.AccountType = AccountType.GameMaster; break;
        }

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminAccounts.AdminAccountsClient(fixture.Channel).UpdateAccountAccessAsync(request, admin).ResponseAsync
        );

        Assert.Equal(StatusCode.FailedPrecondition, error.StatusCode);
    }

    [Fact]
    public async Task UpdateAccountAccess_OnYourOwnAccount_ASafeChangeIsAllowed()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);
        var self = await IdOfAsync(fixture, admin, "admin");

        var summary = await new AdminAccounts.AdminAccountsClient(fixture.Channel)
            .UpdateAccountAccessAsync(new() { AccountId = self, IsLocked = false, CanAccessApi = true, AccountType = AccountType.Administrator }, admin);

        Assert.Equal(self, summary.AccountId);
    }

    [Fact]
    public async Task ChangeAccountPassword_ReplacesThePassword_AndEndsTheSessions()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);
        var player = await CreateAsync(fixture, "player", DomainAccountType.GameMaster, true);
        var playerHeaders = await LoginAsync(fixture, "player");
        var login = new AdminLogin.AdminLoginClient(fixture.Channel);

        var answer = await new AdminAccounts.AdminAccountsClient(fixture.Channel)
            .ChangeAccountPasswordAsync(new() { AccountId = player, NewPassword = "a-brand-new-secret" }, admin);

        Assert.IsType<Empty>(answer);
        Assert.Equal(
            StatusCode.Unauthenticated,
            (await Assert.ThrowsAsync<RpcException>(() => new AdminServer.AdminServerClient(fixture.Channel).GetServerInfoAsync(new(), playerHeaders).ResponseAsync)).StatusCode
        );
        Assert.Equal(
            StatusCode.Unauthenticated,
            (await Assert.ThrowsAsync<RpcException>(() => login.LoginAsync(new() { Username = "player", Password = fixture.Backend.Accounts.Password }).ResponseAsync)).StatusCode
        );
        Assert.False(string.IsNullOrEmpty((await login.LoginAsync(new() { Username = "player", Password = "a-brand-new-secret" })).AccessToken));
    }

    [Theory,
     InlineData(0u, "good-password", StatusCode.InvalidArgument),
     InlineData(1u, "", StatusCode.InvalidArgument),
     InlineData(1u, "bad\0password", StatusCode.InvalidArgument),
     InlineData(4000000u, "good-password", StatusCode.NotFound)]
    public async Task ChangeAccountPassword_BadRequests_AreClassified(uint account, string password, StatusCode expected)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var admin = await LoginAsync(fixture, "admin", DomainAccountType.Administrator);

        var error = await Assert.ThrowsAsync<RpcException>(
            () => new AdminAccounts.AdminAccountsClient(fixture.Channel)
                .ChangeAccountPasswordAsync(new() { AccountId = account, NewPassword = password }, admin)
                .ResponseAsync
        );

        Assert.Equal(expected, error.StatusCode);
    }

    [Theory, InlineData(DomainAccountType.Regular), InlineData(DomainAccountType.GameMaster)]
    public async Task AccountChanges_NonAdministrator_AreDenied(DomainAccountType role)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var headers = await LoginAsync(fixture, "admin", role);
        var accounts = new AdminAccounts.AdminAccountsClient(fixture.Channel);

        Assert.Equal(
            StatusCode.PermissionDenied,
            (await Assert.ThrowsAsync<RpcException>(() => accounts.UpdateAccountAccessAsync(new() { AccountId = 1, IsLocked = true }, headers).ResponseAsync)).StatusCode
        );
        Assert.Equal(
            StatusCode.PermissionDenied,
            (await Assert.ThrowsAsync<RpcException>(() => accounts.ChangeAccountPasswordAsync(new() { AccountId = 1, NewPassword = "x-secret-1" }, headers).ResponseAsync)).StatusCode
        );
    }

    private static async Task<uint> CreateAsync(AdminGrpcFixture fixture, string username, DomainAccountType role, bool api)
    {
        var result = await fixture.Backend.Accounts.Service.CreateAccountAsync(
            new() { Username = username, Password = fixture.Backend.Accounts.Password, AccountType = role, CanAccessApi = api }
        );
        Assert.True(result.Success);

        return result.Account!.Id.Value;
    }

    private static async Task<Metadata> LoginAsync(AdminGrpcFixture fixture, string username, DomainAccountType? create = null)
    {
        if (create is { } role)
        {
            await CreateAsync(fixture, username, role, true);
        }

        var response = await new AdminLogin.AdminLoginClient(fixture.Channel).LoginAsync(
            new() { Username = username, Password = fixture.Backend.Accounts.Password }
        );

        return new() { { "authorization", "Bearer " + response.AccessToken } };
    }

    private static async Task<uint> IdOfAsync(AdminGrpcFixture fixture, Metadata headers, string username)
    {
        var page = await new AdminAccounts.AdminAccountsClient(fixture.Channel).ListAccountsAsync(new(), headers);

        return page.Accounts.Single(account => account.Username == username).AccountId;
    }
}
