using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Types.Speech;
using DryIoc;
using Moongate.Core.Directories;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Persistence.Extensions;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Handlers.BulletinBoards;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Tests.TestSupport.Config;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Handlers.Movement;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Core.Interfaces.Sessions;

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
        container.RegisterInstance(new LineOfSightConfig());
        container.RegisterInstance(new WorldConfig());
        container.RegisterInstance(directories);
        container.RegisterInstance(TimeProvider.System);

        ServerRoleRegistration.Register(container, config, directories);

        Assert.Equal("Moongate", container.Resolve<RealmInstance>().Descriptor.Name);
        Assert.Equal("Città di Luna", container.Resolve<MotdServerIdentity>().ServerName);
    }

    // The commands module of Lua is built by the script engine from the container: what it asks for must be there.
    [Theory, InlineData(ServerMode.Game), InlineData(ServerMode.Standalone)]
    public void Register_TheCommandsModuleOfLua_CanBeBuilt(ServerMode mode)
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = mode };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: mode));

        // The command system is the host's own, registered by Program beside the roles.
        container.Register<ICommandSystemService, Moongate.Server.Services.Commands.CommandSystemService>(Reuse.Singleton);
        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.Contains(
            typeof(Moongate.Server.Ultima.Modules.CommandsModule),
            container.Resolve<Moongate.Scripting.Interfaces.IScriptModuleRegistry>().ModuleTypes
        );
        Assert.NotNull(container.Resolve<Moongate.Server.Ultima.Modules.CommandsModule>(IfUnresolved.Throw));
    }

    // The handler takes the context menus as an optional service: were they not there to build, every menu a
    // client asks for would be dropped in silence.
    [Theory, InlineData(ServerMode.Game), InlineData(ServerMode.Standalone)]
    public void Register_TheExtendedCommandHandler_IsGivenTheContextMenus(ServerMode mode)
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = mode };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: mode));
        // The host's own, registered by Program beside the roles.
        container.RegisterMoongateEventBus();
        container.Register<IEventBusService, Moongate.Server.Services.Events.EventBusService>(Reuse.Singleton);
        container.Register<ICommandSystemService, Moongate.Server.Services.Commands.CommandSystemService>(Reuse.Singleton);

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        var handler = container.Resolve<Moongate.Server.Ultima.Handlers.General.ExtendedCommandPacketHandler>();
        var field = handler.GetType()
            .GetField("_contextMenus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.IsType<Moongate.Server.Ultima.Services.ContextMenuService>(field!.GetValue(handler));
        Assert.Same(
            container.Resolve<Moongate.Server.Ultima.Handlers.Items.UseRequestPacketHandler>(),
            container.Resolve<Moongate.Server.Ultima.Interfaces.IUseService>()
        );
    }

    // The npc module takes the door service as an optional one: were it not there to build, no NPC would open a
    // door and nothing would say why.
    [Fact]
    public void Register_TheNpcModule_IsGivenTheDoorService()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Standalone));
        container.RegisterMoongateEventBus();
        container.Register<IEventBusService, Moongate.Server.Services.Events.EventBusService>(Reuse.Singleton);
        container.Register<ICommandSystemService, Moongate.Server.Services.Commands.CommandSystemService>(Reuse.Singleton);

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        var module = container.Resolve<Moongate.Server.Ultima.Modules.NpcModule>();
        var field = module.GetType()
            .GetField("_doors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.IsType<Moongate.Server.Ultima.Services.NpcDoorService>(field!.GetValue(module));
    }

    // What gives items takes the templates and the tile data as optional services: without them nothing given would
    // join a stack, and nothing would say why.
    [Fact]
    public void Register_WhatGivesItems_KnowsWhatStacks()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Standalone));
        container.RegisterMoongateEventBus();
        container.Register<IEventBusService, Moongate.Server.Services.Events.EventBusService>(Reuse.Singleton);
        container.Register<ICommandSystemService, Moongate.Server.Services.Commands.CommandSystemService>(Reuse.Singleton);

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        var handling = container.Resolve<Moongate.Server.Ultima.Interfaces.IItemHandlingService>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        Assert.NotNull(handling.GetType().GetField("_templates", flags)!.GetValue(handling));
        Assert.NotNull(handling.GetType().GetField("_tiles", flags)!.GetValue(handling));

        // world.statics reads the map: without it a smith could never find the anvil of a smithy.
        var world = container.Resolve<Moongate.Server.Ultima.Modules.WorldModule>();
        Assert.NotNull(world.GetType().GetField("_maps", flags)!.GetValue(world));
    }

    [Fact]
    public void Register_EveryCommand_HasATranslatedDescription()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Standalone));

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        var definitions = container.Resolve<CommandRegistry>()
            .Registrations.Values.Select(registration => registration.Definition)
            .Distinct();
        Assert.All(definitions, definition => Assert.InRange(definition.DescriptionMessage, 30039, 30250));
    }

    [Theory, InlineData(ServerMode.Login), InlineData(ServerMode.Standalone)]
    public void Register_TheLoginRole_AcceptsTheHardwareInfoTheEnhancedClientSendsAtLogin(ServerMode mode)
    {
        // The Enhanced Client sends 0xD9 right after the account login: without a login handler it is disconnected.
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = mode };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: mode));

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.True(container.Resolve<PacketRegistry>().TryGetDescriptor(0xD9, PacketDirection.Incoming, out _));
        Assert.Contains(typeof(ClientHardwareInfoPacket), container.Resolve<LoginPacketHandlerRegistry>().Freeze().Keys);
    }

    // 0x66, 0xD4 and 0x93: what the client sends about a book, a page request right after every opening.
    [Theory, InlineData(0x09), InlineData(0xB8), InlineData(0xBF), InlineData(0xD6), InlineData(0xF0), InlineData(0x66),
     InlineData(0xD4), InlineData(0x93)]
    public void Register_TheTooltipRequests_AreIncomingPacketsTheFramerKnows(int opCode)
    {
        // A packet with a handler but no incoming registration closes the connection when the client sends it.
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Game };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Game));

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.True(container.Resolve<PacketRegistry>().TryGetDescriptor((byte)opCode, PacketDirection.Incoming, out _));
    }

    [Fact]
    public void Register_EveryServiceIsRegisteredOnce()
    {
        // A service registered twice cannot be resolved: the server would not start.
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Standalone));

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        var twice = container.Resolve<List<ServiceRegistrationData>>()
            .GroupBy(registration => registration.ServiceType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Name);
        Assert.Empty(twice);
    }

    [Fact]
    public void Register_TheGumpModule_ResolvesWithItsScriptsLate()
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Standalone));
        container.RegisterMoongateEventBus();

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.NotNull(container.Resolve<GumpModule>());
    }

    [Fact]
    public void Register_TheJail_ResolvesWithItsModuleAndItsCommand()
    {
        // The jail takes the item module, which takes half the world: a cycle would stop the server at startup.
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = ServerMode.Standalone };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: ServerMode.Standalone));
        container.RegisterMoongateEventBus();
        container.Register<IEventBusService, EventBusService>(Reuse.Singleton);

        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.NotNull(container.Resolve<IJailService>());
        Assert.NotNull(container.Resolve<JailModule>());
        Assert.NotNull(container.Resolve<JailCommand>());
        Assert.NotNull(container.Resolve<IBulletinBoardService>());
        Assert.NotNull(container.Resolve<IContainerCapacityService>());
        Assert.NotNull(container.Resolve<DropRequestPacketHandler>());
        Assert.NotNull(container.Resolve<BulletinBoardRequestPacketHandler>());
        Assert.NotNull(container.Resolve<BoardModule>());
        // The death takes the NPC scripts, whose engine takes the mobile module, which takes the death.
        Assert.NotNull(container.Resolve<IDeathService>());
        Assert.NotNull(container.Resolve<ISkillService>());
        Assert.NotNull(container.Resolve<ISkillUseService>());
        Assert.NotNull(container.Resolve<ICombatService>());
        Assert.NotNull(container.Resolve<ICraftService>());
        Assert.NotNull(container.Resolve<MobileModule>());
        Assert.NotNull(container.Resolve<KillCommand>());
        Assert.NotNull(container.Resolve<ResurrectCommand>());
        Assert.NotNull(container.Resolve<IMountService>());
        Assert.NotNull(container.Resolve<MountModule>());
        Assert.NotNull(container.Resolve<IStableService>());
        Assert.NotNull(container.Resolve<IPetService>());
        Assert.NotNull(container.Resolve<ITamingService>());
        Assert.NotNull(container.Resolve<IMobileStateService>());
        Assert.NotNull(container.Resolve<StableModule>());
        Assert.NotNull(container.Resolve<TameCommand>());
        Assert.NotNull(container.Resolve<UseRequestPacketHandler>());
        Assert.NotNull(container.Resolve<MoveRequestPacketHandler>());
    }

    [Theory, InlineData(ServerMode.Login), InlineData(ServerMode.Game), InlineData(ServerMode.Standalone)]
    public void Register_SelectsRoleServicesAndPluginRegistrations(ServerMode mode)
    {
        using var directory = new TemporaryDirectory();
        var directories = new DirectoriesConfig(directory.Path, ["config", "plugins", "scripts"]);
        using var container = new Container();
        var config = new MoongateServerConfig { Mode = mode };
        config.Redis.HandoffSecret = new('x', 32);
        container.RegisterInstance(config);
        container.RegisterInstance(TestConfigDocuments.Empty(directory.Path));
        container.RegisterInstance(directories);
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterMoongatePersistence(config.Persistence.ToOptions(mode: mode));

        container.RegisterMoongateEventBus();
        container.Register<IEventBusService, EventBusService>(Reuse.Singleton);
        ServerRoleRegistration.Register(container, config, directories);
        new MoongateUltimaPlugin().Register(container);

        Assert.True(container.IsRegistered<PingServerService>());
        Assert.True(container.IsRegistered<ISqlBackupService>());
        Assert.IsType<SqlBackupService>(container.Resolve<ISqlBackupService>());
        Assert.Contains(
            container.Resolve<CommandRegistry>().Registrations.Values,
            registration => registration.Definition.DescriptionMessage == 30108
        );
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IGameLoopService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<ISessionService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IWorldSaveService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IEquipmentService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<ITooltipService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<INpcTickService>());
        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<LoginServerService>());
        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<LoginPacketHandlerRegistry>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IDataLoaderService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<MotdServerIdentity>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IMotdService>());
        if (mode != ServerMode.Login)
        {
            Assert.IsType<MotdService>(container.Resolve<IMotdService>());
            Assert.IsType<SectorService>(container.Resolve<ISectorService>());
            Assert.IsType<SpeechService>(container.Resolve<ISpeechService>());
            Assert.IsType<PathfindingService>(container.Resolve<IPathfindingService>());
            Assert.IsType<NpcPathService>(container.Resolve<INpcPathService>());
            Assert.NotNull(container.Resolve<NpcModule>());
            Assert.NotNull(container.Resolve<ItemModule>());
            Assert.NotNull(container.Resolve<WorldModule>());
            Assert.NotNull(container.Resolve<MobileModule>());
            Assert.NotNull(container.Resolve<TargetModule>());
            Assert.Contains(
                "player_region_changed",
                container.Resolve<IScriptModuleRegistry>().EventRegistrations.Select(e => e.Name)
            );
            Assert.Contains(typeof(SpeechKeywordType), container.Resolve<IScriptModuleRegistry>().EnumTypes);
            Assert.Contains("player_say", container.Resolve<IScriptModuleRegistry>().EventRegistrations.Select(e => e.Name));
            Assert.IsType<EffectService>(container.Resolve<IEffectService>());
            Assert.IsType<PublicMoongateService>(container.Resolve<IPublicMoongateService>());
            Assert.NotNull(container.Resolve<MoongatesModule>());
            Assert.IsType<LocationService>(container.Resolve<ILocationService>());
            Assert.NotNull(container.Resolve<LocationsModule>());
            // The places and the gump are optional in its constructor: the container must still hand them over.
            var go = container.Resolve<GoCommand>();
            Assert.All(
                new[] { "_locations", "_gumps" },
                field => Assert.NotNull(
                    typeof(GoCommand).GetField(
                        field,
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    )!.GetValue(go)
                )
            );
            Assert.IsType<ItemHearingService>(container.Resolve<IItemSpeechListener>());
            Assert.NotNull(container.Resolve<EffectModule>());
            Assert.Contains(typeof(EffectGraphicType), container.Resolve<IScriptModuleRegistry>().EnumTypes);
            Assert.IsType<BankService>(container.Resolve<IBankService>());
            Assert.IsType<CharacterEnterWorldService>(container.Resolve<ICharacterEnterWorldService>());
            Assert.NotNull(container.Resolve<BankModule>());
            // The scripts hear of a region change last, after the light, the weather, the music and the season.
            var listeners = container.Resolve<IEnumerable<IRegionChangeListener>>().ToList();
            Assert.Equal(
                [
                    container.Resolve<IWeatherService>(), container.Resolve<ILightService>(),
                    container.Resolve<IMusicService>(), container.Resolve<ISeasonService>()
                ],
                listeners.Take(4)
            );
            // Night sight over the light, the announcer of places, then the scripts last.
            Assert.Same(container.Resolve<IStatBonusService>(), listeners[4]);
            Assert.Same(container.Resolve<IRegionAnnouncer>(), listeners[5]);
            Assert.IsType<RegionEventPublisher>(Assert.Single(listeners.Skip(6)));
            Assert.NotNull(container.Resolve<IMobileService>());
            Assert.Same(container.Resolve<NpcScriptService>(), container.Resolve<INpcThinker>());
            Assert.Same(container.Resolve<NpcScriptService>(), container.Resolve<INpcScriptService>());
            Assert.NotNull(container.Resolve<INpcService>());
            Assert.IsType<NpcSenseService>(container.Resolve<INpcSenseService>());
            Assert.IsType<ItemScriptService>(container.Resolve<IItemScriptService>());
            Assert.IsType<ItemService>(container.Resolve<IItemService>());
            Assert.IsType<ItemDecayQueue>(container.Resolve<IItemDecayQueue>());
            Assert.NotNull(container.Resolve<ItemDecayService>());
            Assert.IsType<MobileService>(container.Resolve<IMobileService>());
            Assert.IsType<NpcHearingService>(container.Resolve<INpcSpeechListener>());
            Assert.Contains(container.ResolveMany<IMetricProvider>(), provider => provider.ProviderName == "npcs");
        }

        Assert.Equal(mode != ServerMode.Game, container.IsRegistered<IAccountService>());
        Assert.True(container.IsRegistered<RedisConnectionService>());
        Assert.True(container.IsRegistered<IRealmCatalog>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<IRealmPresenceService>());
        Assert.Equal(mode != ServerMode.Login, container.IsRegistered<RedisRealmRegistrationService>());
        Assert.True(container.IsRegistered<IHandoffProofService>());
        Assert.True(container.IsRegistered<IGameHandoffStore>());
        Assert.IsType<HandoffProofService>(container.Resolve<IHandoffProofService>());
        Assert.IsType<RedisGameHandoffStore>(container.Resolve<IGameHandoffStore>());

        if (mode != ServerMode.Game)
        {
            Assert.Contains(
                typeof(AccountLoginPacket),
                container.Resolve<LoginPacketHandlerRegistry>().Freeze().Keys
            );
            Assert.Contains(
                typeof(ServerSelectPacket),
                container.Resolve<LoginPacketHandlerRegistry>().Freeze().Keys
            );
        }

        if (mode == ServerMode.Login)
        {
            Assert.False(container.IsRegistered<PacketHandlerRegistry>());
        }
        else
        {
            Assert.DoesNotContain(
                typeof(AccountLoginPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys
            );
            Assert.DoesNotContain(
                typeof(ServerSelectPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys
            );
            Assert.Contains(
                typeof(LoginSeedPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys
            );
            Assert.Contains(
                typeof(CreateCharacterPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys
            );
            Assert.Contains(
                typeof(CreateCharacterEnhancedPacket),
                container.Resolve<PacketHandlerRegistry>().Registrations.Keys
            );
            Assert.All(
                [
                    typeof(ClientHardwareInfoPacket), typeof(AttackRequestPacket), typeof(LiftRequestPacket),
                    typeof(DropRequestPacket), typeof(TextCommandPacket), typeof(EquipRequestPacket),
                    typeof(ResynchronizeRequestPacket), typeof(UnicodeSpeechRequestPacket), typeof(OpenChatWindowPacket),
                    typeof(HelpRequestPacket),
                    typeof(ClientTypePacket), typeof(PublicHouseContentPacket), typeof(GumpResponsePacket)
                ],
                packet => Assert.Contains(packet, container.Resolve<PacketHandlerRegistry>().Registrations.Keys)
            );
            // The host registers the event bus; this test container does not.
            container.RegisterMoongateEventBus();
            var listeners = container.ResolveMany<ISessionClosedListener>().ToList();
            Assert.Equal(11, listeners.Count);
            Assert.Contains(listeners, listener => listener is StatBonusService);
            Assert.Contains(listeners, listener => listener is PoisonService);
            Assert.Contains(listeners, listener => listener is HuePickerService);
            Assert.Contains(listeners, listener => listener is PromptService);
            Assert.Contains(listeners, listener => listener is BankService);
            Assert.Contains(listeners, listener => listener is VendorService);
            Assert.Contains(listeners, listener => listener is TrainingService);
            Assert.Contains(listeners, listener => listener is CharacterLeaveWorldService);
            Assert.Contains(listeners, listener => listener is TargetService);
            Assert.Contains(listeners, listener => listener is GumpService);
            Assert.Contains(listeners, listener => listener is SpellCastService);
            Assert.Contains(
                "character_left_world",
                container.Resolve<IScriptModuleRegistry>().EventRegistrations.Select(registration => registration.Name)
            );
            Assert.True(container.IsRegistered<MoveRequestPacketHandler>());
            Assert.True(container.IsRegistered<UseRequestPacketHandler>());
            Assert.True(container.IsRegistered<LiftRequestPacketHandler>());
            Assert.True(container.IsRegistered<DropRequestPacketHandler>());
            Assert.True(container.IsRegistered<IItemSerialPool>());
            Assert.True(container.IsRegistered<EquipRequestPacketHandler>());
            Assert.False(container.IsRegistered<IgnoredPacketHandler<EquipRequestPacket>>());
            Assert.False(container.IsRegistered<IgnoredPacketHandler<DropRequestPacket>>());
            Assert.False(container.IsRegistered<IgnoredPacketHandler<LiftRequestPacket>>());
            Assert.False(container.IsRegistered<IgnoredPacketHandler<UseRequestPacket>>());
            Assert.False(container.IsRegistered<IgnoredPacketHandler<MoveRequestPacket>>());
            Assert.Contains(typeof(MoveRequestPacket), container.Resolve<PacketHandlerRegistry>().Registrations.Keys);
            Assert.Contains(
                "character_created",
                container.Resolve<IScriptModuleRegistry>().EventRegistrations.Select(registration => registration.Name)
            );
        }

        if (mode != ServerMode.Login)
        {
            Assert.Same(container.Resolve<IRealmCatalog>(), container.Resolve<IRealmPresenceService>());
        }
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
        container.RegisterInstance(new LineOfSightConfig());
        container.RegisterInstance(new WorldConfig());
        container.RegisterInstance(directories);
        container.RegisterInstance(TimeProvider.System);
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
