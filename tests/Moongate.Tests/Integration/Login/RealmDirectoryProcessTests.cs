using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Realms.Api;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Integration.Login;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class RealmDirectoryProcessTests
{
    [Fact]
    public async Task TwoGames_RegisterExpireAndReconnectAfterLoginRestart()
    {
        using var fixture = new ApiHostFixture();
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(3));
        fixture.Registry.RegisterHandler(() => new RegisterRealmHandler(directory));
        fixture.Registry.RegisterHandler(() => new RenewRealmHandler(directory));
        fixture.Registry.RegisterHandler(() => new UnregisterRealmHandler(directory));

        await using var gameA = new RealmRegistrationService(
            fixture.CreateRealmClient("realm-a"), RealmConfig("realm-a", 1, fixture.Config.Port), TimeProvider.System);
        await using var gameB = new RealmRegistrationService(
            fixture.CreateRealmClient("realm-b"), RealmConfig("realm-b", 2, fixture.Config.Port), TimeProvider.System);
        await using var login = fixture.CreateService();
        await login.StartAsync();
        await gameA.StartAsync();
        await gameB.StartAsync();
        await WaitUntilAsync(() => directory.GetAvailable(AccountType.Regular).Count == 2);
        Assert.Equal(["realm-a", "realm-b"], RealmIds(directory));

        await gameA.StopAsync();
        await WaitUntilAsync(() => RealmIds(directory).SequenceEqual(["realm-b"]));
        await login.StopAsync();
        await WaitUntilAsync(() => RealmIds(directory).Length == 0);

        await using var restartedLogin = fixture.CreateService();
        await restartedLogin.StartAsync();
        await WaitUntilAsync(() => RealmIds(directory).SequenceEqual(["realm-b"]));
        await gameB.StopAsync();
        Assert.Empty(RealmIds(directory));
        await restartedLogin.StopAsync();
    }

    private static string[] RealmIds(RealmDirectoryService directory)
        => directory.GetAvailable(AccountType.Regular).Select(realm => realm.RealmId).ToArray();

    private static RealmDirectoryConfig RealmConfig(string realmId, int serverIndex, int apiPort)
        => new()
        {
            RealmId = realmId,
            Name = realmId,
            ServerIndex = serverIndex,
            AdvertisedAddress = "127.0.0.1",
            AdvertisedPort = 2593 + serverIndex,
            LoginApiHost = "localhost",
            LoginApiPort = apiPort,
            ExpectedLoginPeerId = "server",
            HeartbeatIntervalSeconds = 1,
            LeaseDurationSeconds = 3
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
