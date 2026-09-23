using System.Net;
using DryIoc;
using Moongate.Api.Exceptions;
using Moongate.Api.Registry;
using Moongate.Api.Types.Protocol;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Realms.Api;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Realms;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Integration.Api;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class RealmRegistrationApiTests
{
    [Fact]
    public async Task RegisterRealm_UsesAuthenticatedPeerIdentityAndRejectsMismatch()
    {
        using var fixture = new ApiHostFixture();
        fixture.Config.Peers[0].AllowedOperations = new([100, 0x0100, 0x0101, 0x0102]);
        using var container = new Container();
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None).RegisterServices(services =>
        {
            services.RegisterInstance(new MoongateServerConfig { Mode = ServerMode.Login, Api = fixture.Config });
            services.RegisterInstance(fixture.Directories);
            services.RegisterInstance<TimeProvider>(TimeProvider.System);
            services.RegisterInstance<IRealmDirectoryService>(directory);
            ApiServerRegistration.Register(services);
            return services;
        });

        try
        {
            await bootstrap.StartAsync();
            await using var client = fixture.CreateClient(registry =>
            {
                registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>();
                registry.RegisterContract<RenewRealmRequest, RenewRealmResponse>();
                registry.RegisterContract<UnregisterRealmRequest, UnregisterRealmResponse>();
            });
            await using var connection = await client.ConnectAsync(
                container.Resolve<IApiServerService>().Endpoint!, "localhost", "server");
            var instance = Guid.NewGuid().ToByteArray();
            var mismatch = await connection.RequestAsync<RegisterRealmRequest, RegisterRealmResponse>(
                Request("other", instance));
            Assert.False(mismatch.Accepted);
            Assert.Empty(directory.GetAvailable(AccountType.Regular));

            var accepted = await connection.RequestAsync<RegisterRealmRequest, RegisterRealmResponse>(
                Request("client", instance));
            Assert.True(accepted.Accepted);
            Assert.Equal(16, accepted.LeaseId.Length);
            Assert.Equal("client", Assert.Single(directory.GetAvailable(AccountType.Regular)).RealmId);

            var malformed = await connection.RequestAsync<RegisterRealmRequest, RegisterRealmResponse>(
                Request("client", [1]));
            Assert.Equal(RealmRegistrationError.InvalidDescriptor, malformed.Error);
            Assert.Single(directory.GetAvailable(AccountType.Regular));

            var stale = await connection.RequestAsync<RenewRealmRequest, RenewRealmResponse>(
                new() { RealmId = "client", LeaseId = Guid.NewGuid().ToByteArray() });
            Assert.Equal(RealmRegistrationError.StaleLease, stale.Error);
            var renewed = await connection.RequestAsync<RenewRealmRequest, RenewRealmResponse>(
                new() { RealmId = "client", LeaseId = accepted.LeaseId });
            Assert.True(renewed.Accepted);
            var removed = await connection.RequestAsync<UnregisterRealmRequest, UnregisterRealmResponse>(
                new() { RealmId = "client", LeaseId = accepted.LeaseId });
            Assert.True(removed.Accepted);
            Assert.Empty(directory.GetAvailable(AccountType.Regular));
        }
        finally
        {
            await bootstrap.StopAsync();
        }
    }

    [Fact]
    public async Task RegisterRealm_WithoutOperationPermission_IsForbidden()
    {
        using var fixture = new ApiHostFixture();
        using var container = new Container();
        var directory = new RealmDirectoryService(TimeProvider.System, TimeSpan.FromSeconds(15));
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None).RegisterServices(services =>
        {
            services.RegisterInstance(new MoongateServerConfig { Mode = ServerMode.Login, Api = fixture.Config });
            services.RegisterInstance(fixture.Directories);
            services.RegisterInstance<TimeProvider>(TimeProvider.System);
            services.RegisterInstance<IRealmDirectoryService>(directory);
            ApiServerRegistration.Register(services);
            return services;
        });

        try
        {
            await bootstrap.StartAsync();
            await using var client = fixture.CreateClient(registry =>
                registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>());
            await using var connection = await client.ConnectAsync(
                container.Resolve<IApiServerService>().Endpoint!, "localhost", "server");
            var error = await Assert.ThrowsAsync<ApiRemoteException>(() =>
                connection.RequestAsync<RegisterRealmRequest, RegisterRealmResponse>(
                    Request("client", Guid.NewGuid().ToByteArray())));
            Assert.Equal(ApiErrorCode.Forbidden, error.Code);
            Assert.Empty(directory.GetAvailable(AccountType.Regular));
        }
        finally
        {
            await bootstrap.StopAsync();
        }
    }

    [Fact]
    public void RegisterContract_DuplicateOperation_IsRejected()
    {
        var registry = new ApiRegistry();
        registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>();

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterContract<RegisterRealmRequest, RegisterRealmResponse>());
    }

    private static RegisterRealmRequest Request(string realmId, byte[] instanceId)
        => new()
        {
            RealmId = realmId,
            InstanceId = instanceId,
            ServerIndex = 1,
            Name = "Realm A",
            AdvertisedAddress = IPAddress.Loopback.ToString(),
            AdvertisedPort = 2593,
            MinimumAccountType = AccountType.Regular
        };
}
