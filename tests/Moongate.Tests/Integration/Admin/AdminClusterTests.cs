using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Ultima.Data.Account;
using Moongate.Tests.TestSupport.Admin;
using Moongate.Tests.TestSupport.Persistence;
using DomainAccountType = Moongate.Server.Core.Types.Accounts.AccountType;

namespace Moongate.Tests.Integration.Admin;

[Collection(PostgresTestCollection.Name)]
public sealed class AdminClusterTests
{
    [Theory, InlineData("logout"), InlineData("revoke"), InlineData("role"), InlineData("disable"), InlineData("expired")]
    public async Task LoginAndGame_ShareSessionAndRevocationWithoutGameAccountServices(string revoke)
    {
        await using var fixture = await AdminHostFixture.CreateAsync();
        var account = (await fixture.Backend.Accounts.Service.CreateAccountAsync(new AccountCreateOptions
        {
            Username = "admin", Password = fixture.Backend.Accounts.Password, AccountType = DomainAccountType.Administrator, CanAccessApi = true
        })).Account!;
        var login = await new AdminLogin.AdminLoginClient(fixture.LoginChannel).LoginAsync(new()
        {
            Username = "admin", Password = fixture.Backend.Accounts.Password
        });
        var headers = new Metadata { { "authorization", "Bearer " + login.AccessToken } };
        var gameInfo = new AdminServer.AdminServerClient(fixture.GameChannel);
        Assert.Equal("Lilly", (await gameInfo.GetServerInfoAsync(new(), headers)).Codename);
        var accounts = new AdminAccounts.AdminAccountsClient(fixture.LoginChannel);
        await accounts.CreateAccountAsync(new() { Username = "created", Password = fixture.Backend.Accounts.Password }, headers);
        Assert.Equal(2, (await accounts.ListAccountsAsync(new(), headers)).Accounts.Count);
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(() =>
            new AdminAccounts.AdminAccountsClient(fixture.GameChannel).CreateAccountAsync(new(), headers).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(() =>
            new AdminLogin.AdminLoginClient(fixture.GameChannel).LoginAsync(new()).ResponseAsync)).StatusCode);
        switch (revoke)
        {
            case "logout": await new AdminSession.AdminSessionClient(fixture.GameChannel).LogoutAsync(new(), headers); break;
            case "revoke": await fixture.Backend.PeerAuthority.RevokeSessionsAsync(account.Id); break;
            case "role": await fixture.Backend.PeerAuthority.UpdateAccessAsync(account.Id, new() { CanAccessApi = true, AccountType = DomainAccountType.Regular }); break;
            case "disable": await fixture.Backend.PeerAuthority.SetApiAccessAsync("admin", false); break;
            default:
                var digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(login.AccessToken)));
                await fixture.Backend.Redis.Redis.Connection.GetDatabase().KeyDeleteAsync(fixture.Backend.Redis.Prefix + "session:" + digest);
                break;
        }
        foreach (var channel in new[] { fixture.LoginChannel, fixture.GameChannel })
        {
            Assert.Equal(StatusCode.Unauthenticated, (await Assert.ThrowsAsync<RpcException>(() =>
                new AdminServer.AdminServerClient(channel).GetServerInfoAsync(new(), headers).ResponseAsync)).StatusCode);
        }
    }
}
