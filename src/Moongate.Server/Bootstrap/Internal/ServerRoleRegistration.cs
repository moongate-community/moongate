using System.Net;
using System.Security.Cryptography;
using System.Text;
using DryIoc;
using Moongate.Core.Directories;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Commands;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Persistence;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.Ultima;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Login;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>Composes login, game, or combined services in a single host process.</summary>
internal static class ServerRoleRegistration
{
    public static Container Register(Container container, MoongateServerConfig config, DirectoriesConfig directories)
    {
        container.RegisterInstance(config.Mode);
        AdminServiceRegistration.Register(container, config);
        container.RegisterDelegate<RealmDirectoryConfig>(
            resolver => resolver.Resolve<MoongateServerConfig>().RealmDirectory,
            Reuse.Singleton
        );
        container.RegisterDelegate<RedisConfig>(resolver => resolver.Resolve<MoongateServerConfig>().Redis, Reuse.Singleton);
        container.AddMoongateService<RedisConnectionService>(-1000);
        container.RegisterDelegate<RedisRealmDirectoryService>(
            resolver => new(
                resolver.Resolve<RedisConnectionService>(),
                leaseDuration: TimeSpan.FromSeconds(config.RealmDirectory.LeaseDurationSeconds),
                maxRealms: config.RealmDirectory.MaxRealms
            ),
            Reuse.Singleton
        );
        container.RegisterDelegate<IRealmCatalog>(
            resolver => resolver.Resolve<RedisRealmDirectoryService>(),
            Reuse.Singleton
        );
        container.RegisterDelegate<IHandoffProofService>(
            resolver => CreateHandoffProof(resolver.Resolve<RedisConfig>()),
            Reuse.Singleton
        );
        container.Register<IGameHandoffStore, RedisGameHandoffStore>(Reuse.Singleton);

        if ((config.Mode & ServerMode.Game) != 0)
        {
            container.RegisterDelegate<IRealmPresenceService>(
                resolver => resolver.Resolve<RedisRealmDirectoryService>(),
                Reuse.Singleton
            );
            container.RegisterDelegate<RealmInstance>(
                _ => new(CreateRealmDescriptor(config), Guid.NewGuid()),
                Reuse.Singleton
            );
            container.AddMoongateService<RedisRealmRegistrationService>(RedisRealmRegistrationService.StartupPriority);
        }

        switch (config.Mode)
        {
            case ServerMode.Login:
                LoginPacketPipelineRegistration.Register(container);

                break;
            case ServerMode.Game:
                RegisterGame(container, config, directories);

                break;
            case ServerMode.Standalone:
                RegisterGame(container, config, directories);
                LoginPacketPipelineRegistration.Register(container, 110);

                break;
            default:
                throw new InvalidOperationException("Unsupported server mode.");
        }

        return container;
    }

    private static HandoffProofService CreateHandoffProof(RedisConfig config)
    {
        var secret = Encoding.UTF8.GetBytes(config.ResolveHandoffSecret());

        try
        {
            return new(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static void RegisterGame(Container container, MoongateServerConfig config, DirectoriesConfig directories)
    {
        container.RegisterInstance(config.WorldSave.ToOptions());
        container.RegisterInstance(config.Scripting.ToOptions(directories["scripts"]));
        container.RegisterInstance(new GameLoopOptions());
        container.RegisterInstance(new TimerWheelOptions());
        container.RegisterDelegate<ITimerService>(resolver => resolver.Resolve<TimerWheelService>(), Reuse.Singleton);
        container.Register<PersistenceOperationBarrier>(Reuse.Singleton);
        container.RegisterDelegate<IPersistenceOperationBarrier>(
            resolver => resolver.Resolve<PersistenceOperationBarrier>(),
            Reuse.Singleton
        );
        container.AddMoongateService<TimerWheelService>(-900)
                 .AddMoongateService<IGameLoopService, GameLoopService>(-800)
                 .AddMoongateService<IUltimaDataService, UltimaDataService>(-10)
                 .AddMoongateService<IWorldSaveService, WorldSaveService>(WorldSaveService.StartupPriority)
                 .AddMoongateService<ISessionService, SessionService>()
                 .AddMoongateService<IScriptEngine, LuaScriptEngineService>(LuaScriptEngineService.StartupPriority)
                 .AddScriptModule<LogModule>()
                 .RegisterCommand<ScriptCommand>(
                     "script",
                     "Reloads a script file or prints the engine's counters: script reload <file> | script metrics."
                 )
                 .RegisterPacketHandler<PingPacket, PingPacketHandler>()
                 .RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        container.AddMetricProvider<GameLoopMetricsProvider>()
                 .AddMetricProvider<TimerMetricsProvider>()
                 .AddMetricProvider<SessionMetricsProvider>();
        PacketPipelineRegistration.Register(container);
    }

    private static RealmDescriptor CreateRealmDescriptor(MoongateServerConfig config)
    {
        var settings = config.RealmDirectory;
        var address = string.IsNullOrWhiteSpace(settings.AdvertisedAddress)
                          ? IPAddress.Loopback
                          : IPAddress.Parse(settings.AdvertisedAddress);
        var shardName = config.Shard.ShardName;
        var defaultName = shardName is { Length: > 0 and <= 32 } &&
                          shardName.All(character => character is >= ' ' and <= '~')
                              ? shardName
                              : "Moongate";

        return new(
            string.IsNullOrWhiteSpace(settings.RealmId) ? "local" : settings.RealmId,
            checked((ushort)settings.ServerIndex),
            string.IsNullOrWhiteSpace(settings.Name) ? defaultName : settings.Name,
            address,
            checked((ushort)(settings.AdvertisedPort == 0 ? config.Network.GamePort : settings.AdvertisedPort)),
            settings.MinimumAccountType
        );
    }
}
