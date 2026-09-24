using DryIoc;
using Moongate.Api.Registry;
using Moongate.Core.Directories;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Persistence.Extensions;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class ServerRoleRegistrationTests
{
    [Fact]
    public void Register_StandaloneWithUnicodeShardName_UsesWireSafeDefaultRealmName()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Shard.ShardName = "Città di Luna";
        container.RegisterInstance(config);
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);

        ServerRoleRegistration.Register(container, config, directories);

        Assert.Equal("Moongate", Assert.Single(container.Resolve<IRealmDirectoryService>()
            .GetAvailable(AccountType.Regular)).Name);
    }

    [Theory, InlineData(ServerMode.Login), InlineData(ServerMode.Game), InlineData(ServerMode.Standalone)]
    public void Register_SelectsRoleServicesAndPluginRegistrations(ServerMode mode)
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = mode };
        container.RegisterInstance(config);
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: mode));

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IGameLoopService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<ISessionService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IWorldSaveService>());
        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<LoginServerService>());
        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<LoginPacketHandlerRegistry>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IDataLoaderService>());
        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<IAccountService>());
        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<IRealmDirectoryService>());
        Assert.True(container.IsRegistered<RedisConnectionService>());
        Assert.True(container.IsRegistered<IRealmCatalog>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IRealmPresenceService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<RedisRealmRegistrationService>());
        Assert.Equal(mode == ServerMode.Login ? 3 : 0, container.Resolve<ApiRegistry>().HandlerCount);
        if (mode != ServerMode.Game)
        {
            Assert.Contains(typeof(AccountLoginPacket),
                container.Resolve<LoginPacketHandlerRegistry>().Freeze().Keys);
            Assert.Contains(typeof(ServerSelectPacket),
                container.Resolve<LoginPacketHandlerRegistry>().Freeze().Keys);
        }

        if (mode == ServerMode.Login)
        {
            Assert.False(container.IsRegistered<PacketHandlerRegistry>());
        }
        else
        {
            Assert.DoesNotContain(typeof(AccountLoginPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys);
            Assert.DoesNotContain(typeof(ServerSelectPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys);
            Assert.Contains(typeof(LoginSeedPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys);
        }
        Assert.Equal(mode == ServerMode.Standalone,
            mode != ServerMode.Game && container.Resolve<IRealmDirectoryService>()
                .GetAvailable(AccountType.Regular).Count == 1);
    }

    [Fact]
    public void Register_StandaloneKeepsRoleTransportInstancesAndPortsSeparate()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig
        {
            Mode = ServerMode.Standalone,
            Network = new() { ListenAddress = "127.0.0.1", LoginPort = 2593, GamePort = 2595 }
        };
        container.RegisterInstance(config);
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: config.Mode));

        ServerRoleRegistration.Register(container, config, directories);

        Assert.NotSame(container.Resolve<IConnectionService>(), container.Resolve<ILoginConnectionService>());
        Assert.NotSame(container.Resolve<IPacketSendService>(), container.Resolve<ILoginPacketSendService>());
        var game = Assert.IsType<NetworkService>(container.Resolve<INetworkService>());
        var login = Assert.IsType<NetworkService>(container.Resolve<ILoginNetworkService>());
        Assert.NotSame(game, login);
        Assert.Equal(2593, Assert.Single(login.Listeners).Endpoint.Port);
        Assert.Equal(2595, Assert.Single(game.Listeners).Endpoint.Port);
    }
}
