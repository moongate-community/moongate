using System.Net.Sockets;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;
using Npgsql;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Tests.Integration.Admin;

[Collection(PostgresTestCollection.Name)]
public sealed class AdminGrpcServicesTests
{
    [Theory, InlineData(true, StatusCode.Unavailable), InlineData(false, StatusCode.Internal)]
    public async Task ListAccounts_ProviderFailure_ReturnsSafeClassifiedStatus(bool transient, StatusCode expected)
    {
        DelayedAccountService? controlled = null;
        await using var fixture = await AdminGrpcFixture.CreateAsync(decorateAccounts: service => controlled = new(service));
        var headers = await LoginAsync(fixture, DomainAccountType.Administrator);
        Exception provider = transient
                                 ? new NpgsqlException(
                                     "private provider details",
                                     new SocketException((int)SocketError.ConnectionRefused)
                                 )
                                 : new PostgresException(
                                     "private SQL details",
                                     "ERROR",
                                     "ERROR",
                                     PostgresErrorCodes.UndefinedTable
                                 );
        controlled!.ListFailure = new InvalidOperationException("private wrapper details", provider);
        var error = await Assert.ThrowsAsync<RpcException>(
                        () => new AdminAccounts.AdminAccountsClient(fixture.Channel)
                              .ListAccountsAsync(new(), headers)
                              .ResponseAsync
                    );
        Assert.Equal(expected, error.StatusCode);
        Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public async Task CreateAccount_CancellationAfterCommit_RetryIsDuplicate()
    {
        DelayedAccountService? delayed = null;
        await using var fixture = await AdminGrpcFixture.CreateAsync(decorateAccounts: service => delayed = new(service));
        var headers = await LoginAsync(fixture, DomainAccountType.Administrator);
        var accounts = new AdminAccounts.AdminAccountsClient(fixture.Channel);
        using var cancellation = new CancellationTokenSource();
        var request = new CreateAccountRequest { Username = "committed", Password = fixture.Backend.Accounts.Password };
        var call = accounts.CreateAccountAsync(request, headers, cancellationToken: cancellation.Token).ResponseAsync;
        await delayed!.Committed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        Assert.Equal(StatusCode.Cancelled, (await Assert.ThrowsAsync<RpcException>(() => call)).StatusCode);
        delayed.DelayResponse = false;
        Assert.Equal(
            StatusCode.AlreadyExists,
            (await Assert.ThrowsAsync<RpcException>(() => accounts.CreateAccountAsync(request, headers).ResponseAsync))
            .StatusCode
        );
        Assert.Equal(2, (await accounts.ListAccountsAsync(new(), headers)).Accounts.Count);
    }

    [Fact]
    public async Task Calls_AtCapacityOrStopping_AreRejectedBeforeAuthentication()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync(concurrency: 1);
        var headers = await LoginAsync(fixture, DomainAccountType.Administrator);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Backend.Store.BeforeIssue = async () =>
                                            {
                                                entered.TrySetResult();
                                                await release.Task;
                                            };
        var pending = new AdminLogin.AdminLoginClient(fixture.Channel).LoginAsync(
                                                                          new()
                                                                          {
                                                                              Username = "admin",
                                                                              Password = fixture.Backend.Accounts.Password
                                                                          }
                                                                      )
                                                                      .ResponseAsync;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            var client = new AdminServer.AdminServerClient(fixture.Channel);
            Assert.Equal(
                StatusCode.ResourceExhausted,
                (await Assert.ThrowsAsync<RpcException>(() => client.GetServerInfoAsync(new(), headers).ResponseAsync))
                .StatusCode
            );
            fixture.Gate.StopAccepting();
            Assert.Equal(
                StatusCode.Unavailable,
                (await Assert.ThrowsAsync<RpcException>(() => client.GetServerInfoAsync(new(), headers).ResponseAsync))
                .StatusCode
            );
        }
        finally
        {
            release.TrySetResult();
            await pending;
        }
    }

    [Theory, InlineData(DomainAccountType.Regular), InlineData(DomainAccountType.GameMaster)]
    public async Task AccountManagement_NonAdministrator_ListAndRevocationAreDenied(DomainAccountType role)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var headers = await LoginAsync(fixture, role);
        Assert.Equal(
            StatusCode.PermissionDenied,
            (await Assert.ThrowsAsync<RpcException>(
                 () =>
                     new AdminAccounts.AdminAccountsClient(fixture.Channel).ListAccountsAsync(new(), headers).ResponseAsync
             )).StatusCode
        );
        Assert.Equal(
            StatusCode.PermissionDenied,
            (await Assert.ThrowsAsync<RpcException>(
                 () =>
                     new AdminAccountSessions.AdminAccountSessionsClient(fixture.Channel)
                         .RevokeAccountSessionsAsync(new() { AccountId = 1 }, headers)
                         .ResponseAsync
             )).StatusCode
        );
        Assert.Equal(
            "Lilly",
            (await new AdminServer.AdminServerClient(fixture.Channel).GetServerInfoAsync(new(), headers)).Codename
        );
    }

    [Theory, InlineData(""), InlineData("   "), InlineData("bad\0name")]
    public async Task Login_InvalidInput_ReturnsInvalidArgument(string username)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        Assert.Equal(
            StatusCode.InvalidArgument,
            (await Assert.ThrowsAsync<RpcException>(
                 () =>
                     new AdminLogin.AdminLoginClient(fixture.Channel)
                         .LoginAsync(new() { Username = username, Password = "unused" })
                         .ResponseAsync
             )).StatusCode
        );
    }

    [Theory, InlineData(DomainAccountType.Regular), InlineData(DomainAccountType.GameMaster)]
    public async Task CreateAccount_NonAdministrator_IsDeniedWithoutInsertion(DomainAccountType role)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var headers = await LoginAsync(fixture, role);
        var client = new AdminAccounts.AdminAccountsClient(fixture.Channel);
        var error = await Assert.ThrowsAsync<RpcException>(
                        () => client.CreateAccountAsync(
                                        new()
                                        {
                                            Username = "blocked", Password = fixture.Backend.Accounts.Password
                                        },
                                        headers
                                    )
                                    .ResponseAsync
                    );
        Assert.Equal(StatusCode.PermissionDenied, error.StatusCode);
        Assert.Single(await fixture.Backend.Accounts.Service.ListAccountsAsync());
    }

    [Fact]
    public async Task Administrator_CreateListInfoLogout_UsesSafeContractsAndIdempotentLogout()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var headers = await LoginAsync(fixture, DomainAccountType.Administrator);
        var client = new AdminAccounts.AdminAccountsClient(fixture.Channel);
        var request = new CreateAccountRequest { Username = "new-user", Password = fixture.Backend.Accounts.Password };
        var account = await client.CreateAccountAsync(request, headers);
        Assert.Equal(AccountType.Regular, account.AccountType);
        Assert.False(account.CanAccessApi);
        Assert.True(account.AccountId > 0);
        Assert.True(account.CreatedAt.Seconds > 0);
        Assert.Equal(
            StatusCode.AlreadyExists,
            (await Assert.ThrowsAsync<RpcException>(() => client.CreateAccountAsync(request, headers).ResponseAsync))
            .StatusCode
        );
        var page = await client.ListAccountsAsync(new() { PageSize = 1 }, headers);
        Assert.Single(page.Accounts);
        Assert.NotEqual(0u, page.NextAfterAccountId);
        var next = await client.ListAccountsAsync(new() { PageSize = 1, AfterAccountId = page.NextAfterAccountId }, headers);
        Assert.Equal("new-user", Assert.Single(next.Accounts).Username);
        Assert.Equal(0u, next.NextAfterAccountId);
        var info = new AdminServer.AdminServerClient(fixture.Channel);
        Assert.Equal("Lilly", (await info.GetServerInfoAsync(new(), headers)).Codename);
        var sessions = new AdminSession.AdminSessionClient(fixture.Channel);
        await sessions.LogoutAsync(new(), headers);
        await sessions.LogoutAsync(new(), headers);
        Assert.Equal(
            StatusCode.Unauthenticated,
            (await Assert.ThrowsAsync<RpcException>(() => info.GetServerInfoAsync(new(), headers).ResponseAsync)).StatusCode
        );
    }

    [Theory, InlineData("missing"), InlineData("malformed"), InlineData("duplicate")]
    public async Task GetServerInfo_InvalidAuthorization_Rejects(string kind)
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var valid = await LoginAsync(fixture, DomainAccountType.Regular);
        var headers = kind switch
        {
            "missing"   => new Metadata(),
            "duplicate" => new Metadata { valid[0], valid[0] },
            _           => new Metadata { { "authorization", "Bearer bad" } }
        };
        Assert.Equal(
            StatusCode.Unauthenticated,
            (await Assert.ThrowsAsync<RpcException>(
                 () =>
                     new AdminServer.AdminServerClient(fixture.Channel).GetServerInfoAsync(new(), headers).ResponseAsync
             )).StatusCode
        );
    }

    [Fact]
    public async Task AccountRequests_InvalidValues_RejectBeforeDatabaseMutation()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var headers = await LoginAsync(fixture, DomainAccountType.Administrator);
        var client = new AdminAccounts.AdminAccountsClient(fixture.Channel);

        foreach (var type in new[] { AccountType.Unspecified, (AccountType)99 })
        {
            Assert.Equal(
                StatusCode.InvalidArgument,
                (await Assert.ThrowsAsync<RpcException>(
                     () => client.CreateAccountAsync(
                                     new()
 {
                                         Username = "new", Password = fixture.Backend.Accounts.Password, AccountType = type
                                     },
                                     headers
                                 )
                                 .ResponseAsync
                 )).StatusCode
            );
        }
        Assert.Equal(
            StatusCode.InvalidArgument,
            (await Assert.ThrowsAsync<RpcException>(
                 () => client.ListAccountsAsync(new() { PageSize = 201 }, headers).ResponseAsync
             )).StatusCode
        );
        Assert.Equal(
            StatusCode.InvalidArgument,
            (await Assert.ThrowsAsync<RpcException>(
                 () =>
                     new AdminAccountSessions.AdminAccountSessionsClient(fixture.Channel)
                         .RevokeAccountSessionsAsync(new(), headers)
                         .ResponseAsync
             )).StatusCode
        );
        Assert.Single(await fixture.Backend.Accounts.Service.ListAccountsAsync());
    }

    [Fact]
    public async Task GetServerInfo_RedisUnavailable_ReturnsUnavailable()
    {
        await using var fixture = await AdminGrpcFixture.CreateAsync();
        var headers = await LoginAsync(fixture, DomainAccountType.Administrator);
        await fixture.Backend.Redis.Redis.StopAsync();

        try
        {
            Assert.Equal(
                StatusCode.Unavailable,
                (await Assert.ThrowsAsync<RpcException>(
                     () =>
                         new AdminServer.AdminServerClient(fixture.Channel).GetServerInfoAsync(new(), headers).ResponseAsync
                 )).StatusCode
            );
        }
        finally { await fixture.Backend.Redis.Redis.StartAsync(); }
    }

    private static async Task<Metadata> LoginAsync(AdminGrpcFixture fixture, DomainAccountType role)
    {
        var result = await fixture.Backend.Accounts.Service.CreateAccountAsync(
                         new()
                         {
                             Username = "admin", Password = fixture.Backend.Accounts.Password, AccountType = role,
                             CanAccessApi = true
                         }
                     );
        Assert.True(result.Success);
        var response = await new AdminLogin.AdminLoginClient(fixture.Channel).LoginAsync(
                           new()
                           {
                               Username = "admin", Password = fixture.Backend.Accounts.Password
                           }
                       );

        return new() { { "authorization", "Bearer " + response.AccessToken } };
    }
}
