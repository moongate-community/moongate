using System.Net;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LoginAccountFlowTests
{
    [Fact]
    public async Task AuthenticateAsync_RegularSeesOnlyEligibleRealms()
    {
        var accounts = new RecordingAccountService { LoginResult = Account(AccountType.Regular) };
        var directory = Directory();
        var flow = new LoginAccountFlow(accounts, directory);

        var result = await flow.AuthenticateAsync("user", "password", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new Serial(42), result.AccountId);
        Assert.Equal([2], result.Servers.Select(server => server.ServerIndex));
    }

    [Fact]
    public async Task AuthenticateAsync_AdministratorSeesAllRealmsInIndexOrder()
    {
        var flow = new LoginAccountFlow(
            new RecordingAccountService { LoginResult = Account(AccountType.Administrator) }, Directory());

        var result = await flow.AuthenticateAsync("admin", "password", CancellationToken.None);

        Assert.Equal([1, 2, 4], result.Servers.Select(server => server.ServerIndex));
        Assert.All(result.Servers, server => Assert.Equal(IPAddress.Loopback, server.Address));
    }

    [Fact]
    public async Task AuthenticateAsync_NoEligibleRealm_DeniesCommunicationProblem()
    {
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        directory.RegisterLocal(new RealmDescriptor("staff", 1, "Staff", IPAddress.Loopback, 2593,
            AccountType.GameMaster));
        var flow = new LoginAccountFlow(
            new RecordingAccountService { LoginResult = Account(AccountType.Regular) }, directory);

        var result = await flow.AuthenticateAsync("user", "password", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(LoginDeniedReason.CommunicationProblem, result.DenialReason);
        Assert.Equal(Serial.Zero, result.AccountId);
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidCredentialsAndUnknownLevel_AreDenied()
    {
        var accounts = new RecordingAccountService();
        var flow = new LoginAccountFlow(accounts, Directory());
        var invalid = await flow.AuthenticateAsync("user", "bad", CancellationToken.None);
        Assert.Equal(LoginDeniedReason.InvalidCredentials, invalid.DenialReason);
        accounts.LoginResult = Account((AccountType)99);
        var unknownLevel = await flow.AuthenticateAsync("user", "password", CancellationToken.None);
        Assert.Equal(LoginDeniedReason.CommunicationProblem, unknownLevel.DenialReason);
    }

    [Fact]
    public async Task AuthenticateAsync_UnavailableRealmCatalog_DeniesCommunicationProblem()
    {
        await using var redis = new RedisConnectionService(new RedisConfig
        {
            ConnectionString = "127.0.0.1:1",
            HandoffSecret = new string('x', 32)
        });
        var directory = new RedisRealmDirectoryService(redis);
        var flow = new LoginAccountFlow(
            new RecordingAccountService { LoginResult = Account(AccountType.Regular) }, directory);

        var result = await flow.AuthenticateAsync("user", "password", CancellationToken.None);

        Assert.Equal(LoginDeniedReason.CommunicationProblem, result.DenialReason);
    }

    private static AccountEntity Account(AccountType accountType)
        => new() { Id = new Serial(42), Username = "user", AccountType = accountType };

    private static RealmDirectoryService Directory()
    {
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        directory.RegisterLocal(new RealmDescriptor("admin", 4, "Admin", IPAddress.Loopback, 2595,
            AccountType.Administrator));
        directory.RegisterLocal(new RealmDescriptor("regular", 2, "Regular", IPAddress.Loopback, 2593,
            AccountType.Regular));
        directory.RegisterLocal(new RealmDescriptor("staff", 1, "Staff", IPAddress.Loopback, 2594,
            AccountType.GameMaster));
        return directory;
    }
}
