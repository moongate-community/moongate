using DryIoc;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Tests.TestSupport.Api;
using Moongate.Tests.TestSupport.Environment;

namespace Moongate.Tests.Integration.Login;

[Collection(EnvironmentTestsCollection.Name)]
public sealed class ServerModeBootstrapTests
{
    [Fact]
    public async Task StartAsync_LoginRole_StartsListenerAndApiWithoutWorldServices()
    {
        using var fixture = new ApiHostFixture();
        using var container = new Container();
        var config = new MoongateServerConfig
        {
            Mode = ServerMode.Login,
            Api = fixture.Config,
            Network = new() { ListenAddress = "127.0.0.1", LoginPort = 0 }
        };
        container.RegisterInstance(config);
        container.RegisterInstance(fixture.Directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(new PostgreSqlPersistenceOptions());
        ServerRoleRegistration.Register(container, config, fixture.Directories);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        try
        {
            await bootstrap.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(container.Resolve<IApiServerService>().Endpoint);
            Assert.False(container.IsRegistered<IGameLoopService>());
            Assert.False(container.IsRegistered<ISessionService>());
            Assert.False(container.IsRegistered<IWorldSaveService>());
        }
        finally
        {
            await bootstrap.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
