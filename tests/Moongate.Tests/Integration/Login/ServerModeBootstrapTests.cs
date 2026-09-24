using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Extensions;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Login;

public sealed class ServerModeBootstrapTests
{
    [Fact]
    public async Task StartAsync_LoginRole_StartsOnlyLoginListenerWithoutWorldServices()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig
        {
            Mode = ServerMode.Login,
            Network = new() { ListenAddress = "127.0.0.1", LoginPort = 0 },
            Redis = new()
            {
                ConnectionString = Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING") ??
                                   "localhost:6379",
                HandoffSecret = new string('x', 32)
            }
        };
        container.RegisterInstance(config);
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(new PostgreSqlPersistenceOptions());
        ServerRoleRegistration.Register(container, config, directories);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        try
        {
            await bootstrap.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var login = Assert.IsType<NetworkService>(container.Resolve<ILoginNetworkService>());
            Assert.Single(login.Listeners);
            Assert.False(container.IsRegistered<INetworkService>());
            Assert.False(container.IsRegistered<IGameLoopService>());
            Assert.False(container.IsRegistered<ISessionService>());
            Assert.False(container.IsRegistered<IWorldSaveService>());
        }
        finally
        {
            await bootstrap.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
