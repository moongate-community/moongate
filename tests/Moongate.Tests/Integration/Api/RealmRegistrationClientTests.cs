using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Api;
using Moongate.Server.Services.Api.Internal;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Realms.Api;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Integration.Api;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class RealmRegistrationClientTests
{
    [Fact]
    public async Task StartAsync_LoginStartsLater_RegistersRenewsAndUnregisters()
    {
        using var fixture = new ApiHostFixture();
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(3));
        fixture.Config.Peers[0].AllowedOperations = new([100, 0x0100, 0x0101, 0x0102]);
        fixture.Registry.RegisterHandler(() => new RegisterRealmHandler(directory));
        fixture.Registry.RegisterHandler(() => new RenewRealmHandler(directory));
        fixture.Registry.RegisterHandler(() => new UnregisterRealmHandler(directory));
        var client = fixture.CreateClient(registry =>
        {
            registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>();
            registry.RegisterContract<RenewRealmRequest, RenewRealmResponse>();
            registry.RegisterContract<UnregisterRealmRequest, UnregisterRealmResponse>();
        });
        var config = new RealmDirectoryConfig
        {
            RealmId = "client", Name = "Realm A", ServerIndex = 1,
            AdvertisedAddress = "127.0.0.1", AdvertisedPort = 2593,
            LoginApiHost = "localhost", LoginApiPort = fixture.Config.Port,
            ExpectedLoginPeerId = "server", HeartbeatIntervalSeconds = 1,
            LeaseDurationSeconds = 3
        };
        await using var registration = new RealmRegistrationService(client, config, TimeProvider.System);
        await registration.StartAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Empty(directory.GetAvailable(AccountType.Regular));

        await using var api = fixture.CreateService();
        await api.StartAsync();
        await WaitUntilAsync(() => directory.GetAvailable(AccountType.Regular).Count == 1);
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.Single(directory.GetAvailable(AccountType.Regular));

        await api.StopAsync();
        await Task.Delay(TimeSpan.FromSeconds(4));
        Assert.Empty(directory.GetAvailable(AccountType.Regular));
        await using var restarted = fixture.CreateService();
        await restarted.StartAsync();
        await WaitUntilAsync(() => directory.GetAvailable(AccountType.Regular).Count == 1);

        await registration.StopAsync();
        Assert.Empty(directory.GetAvailable(AccountType.Regular));
        await restarted.StopAsync();
    }

    [Fact]
    public async Task ApiClientFactory_DisabledLocalListener_StillConnectsWithMutualTls()
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.Peers[0].CertificateSha256 = fixture.ServerFingerprint;
        await using var api = fixture.CreateService();
        await api.StartAsync();
        var outbound = new ApiConfig
        {
            Enabled = false,
            CertificatePath = fixture.Config.CertificatePath,
            CertificatePasswordEnvironmentVariable = fixture.Config.CertificatePasswordEnvironmentVariable,
            TrustedRootPaths = fixture.Config.TrustedRootPaths,
            Peers = [new()
            {
                CertificateSha256 = fixture.ServerFingerprint,
                PeerId = "server",
                AllowedOperations = new([0x0100, 0x0101, 0x0102])
            }]
        };
        await using var client = ApiClientFactory.Create(outbound, fixture.Directories, TimeProvider.System);
        await using var connection = await client.ConnectAsync(api.Endpoint!, "localhost", "server");

        Assert.Equal("server", connection.Peer.PeerId);
        await api.StopAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
