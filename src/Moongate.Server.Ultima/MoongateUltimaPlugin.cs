using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Persistence.Extensions;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Data.Titles;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Handlers.Movement;
using Moongate.Server.Ultima.Handlers.Targeting;
using Moongate.Server.Ultima.Handlers.Tooltips;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Interfaces.Titles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Diagnostics;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Server.Ultima.Services.Titles;

namespace Moongate.Server.Ultima;

public class MoongateUltimaPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata
        => new(
            "com.github.moongate-community.moongate.plugins.ultima",
            "Moongate Ultima",
            new(1, 0),
            "squid",
            "Provides the core Ultima Online server functionality."
        );

    public void Register(Container container)
    {
        container.AddUltimaConfig();

        var directoriesConfig = container.Resolve<DirectoriesConfig>();

        // Configuration files
        directoriesConfig.CreateDirectoryIfNotExists("data/");

        directoriesConfig.CreateDirectoryIfNotExists("templates");
        directoriesConfig.CreateDirectoryIfNotExists("templates/mobiles/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/items/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/loots/");

        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new Point2DTomlConverter());
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new Rectangle2DTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());

        var mode = container.IsRegistered<ServerMode>() ? container.Resolve<ServerMode>() : ServerMode.Standalone;

        if ((mode & ServerMode.Login) != 0)
        {
            container.AddPersistenceAuth<AccountEntity>();
            container.AddMoongateService<IAccountService, AccountService>();
            container.Register<IAccountAdminAccessService, AccountAdminAccessService>(Reuse.Singleton);
            container.Register<LoginAccountFlow>(Reuse.Singleton);
            container.RegisterCommand<AccountCommand>(
                "account",
                "Creates an account: account create <username> <password> [Regular|GameMaster|Administrator]. Console provisioning: account api-access <username> <on|off>.",
                CommandSourceType.Console | CommandSourceType.InGame,
                descriptionMessage: CommandMessages.AccountDescription
            );

            container.RegisterLoginPacketHandler<PingPacket, LoginRolePingPacketHandler>();
            container.RegisterLoginPacketHandler<LoginSeedPacket, LoginRoleSeedPacketHandler>();
            container.RegisterLoginPacketHandler<ClientVersionPacket, LoginRoleClientVersionPacketHandler>();
            container.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
            container.RegisterLoginPacketHandler<ServerSelectPacket, LoginRoleServerSelectPacketHandler>();
        }

        if ((mode & ServerMode.Game) != 0)
        {
            container.Register<IMotdVariableRegistry, MotdVariableRegistry>(Reuse.Singleton);
            MotdRenderer.RegisterBuiltins(container.Resolve<IMotdVariableRegistry>());
            container.Register<MotdRenderer>(Reuse.Singleton);
            container.Register<IMotdService, MotdService>(Reuse.Singleton);
            container.Register<IFameKarmaTitleService, FameKarmaTitleService>(Reuse.Singleton);
            container.AddUltimaDataLoader<MapLoader, MapContent>(0);
            container.AddUltimaDataLoader<StartingCitiesLoader, StartingCityContent>(1);
            container.AddUltimaDataLoader<SkillsLoader, SkillContent>(2);
            container.AddUltimaDataLoader<ProfessionsLoader, ProfessionContent>(3);
            container.AddUltimaDataLoader<RacesLoader, RaceContent>(4);
            container.AddUltimaDataLoader<BannedNamesLoader, BannedNamesContent>(5);
            container.AddUltimaDataLoader<ContainersLoader, ContainerContent>(6);
            container.AddUltimaDataLoader<BodiesLoader, BodyContent>(7);
            container.AddUltimaDataLoader<WeatherLoader, WeatherContent>(8);
            container.AddUltimaDataLoader<RegionsLoader, RegionContent>(9);
            container.AddUltimaDataLoader<MessagesLoader, MessageContent>(10);
            container.AddUltimaDataLoader<NamesLoader, NameList>(11);
            container.AddUltimaDataLoader<ItemTemplatesLoader, ItemTemplate>(12);
            container.AddUltimaDataLoader<StartingItemsLoader, StartingItemSet>(13);
            container.AddUltimaDataLoader<LootTemplatesLoader, LootTemplate>(14);
            container.AddUltimaDataLoader<MobileTemplatesLoader, MobileTemplate>(15);
            container.AddUltimaDataLoader<MotdLoader, MotdLine>(16);
            container.AddUltimaDataLoader<TitlesLoader, FameKarmaTitle>(17);

            container.RegisterPacketHandler<LoginSeedPacket, LoginSeedPacketHandler>();
            container.RegisterAsyncPacketHandler<GameLoginPacket, GameLoginPacketHandler>();
            container.RegisterIncomingPacket<CreateCharacterPacket>();
            container.RegisterIncomingPacket<CreateCharacterEnhancedPacket>();
            container.RegisterIncomingPacket<DeleteCharacterPacket>();
            container.RegisterIncomingPacket<PlayCharacterPacket>();
            container.RegisterAsyncPacketHandler<PlayCharacterPacket, PlayCharacterPacketHandler>();
            container.RegisterIncomingPacket<MoveRequestPacket>();
            container.RegisterPacketHandler<MoveRequestPacket, MoveRequestPacketHandler>();
            container.RegisterIncomingPacket<UseRequestPacket>();
            container.RegisterPacketHandler<UseRequestPacket, UseRequestPacketHandler>();
            container.RegisterIncomingPacket<AsciiSpeechRequestPacket>();
            container.RegisterAsyncPacketHandler<AsciiSpeechRequestPacket, SpeechRequestPacketHandler>();
            container.RegisterIncomingPacket<UnicodeSpeechRequestPacket>();
            container.RegisterAsyncPacketHandler<UnicodeSpeechRequestPacket, SpeechRequestPacketHandler>();
            container.RegisterIncomingPacket<TargetResponsePacket>();
            container.RegisterPacketHandler<TargetResponsePacket, TargetResponsePacketHandler>();
            container.RegisterIncomingPacket<LiftRequestPacket>();
            container.RegisterPacketHandler<LiftRequestPacket, LiftRequestPacketHandler>();
            container.RegisterIncomingPacket<DropRequestPacket>();
            container.RegisterPacketHandler<DropRequestPacket, DropRequestPacketHandler>();
            container.RegisterIncomingPacket<EquipRequestPacket>();
            container.RegisterPacketHandler<EquipRequestPacket, EquipRequestPacketHandler>();

            // Sent by the client around and after entering the world; recognised so it is not disconnected.
            RegisterIgnoredPacket<ClientHardwareInfoPacket>(container);
            container.RegisterIncomingPacket<LookRequestPacket>();
            container.RegisterPacketHandler<LookRequestPacket, LookRequestPacketHandler>();
            RegisterIgnoredPacket<MobileQueryPacket>(container);
            RegisterIgnoredPacket<WarModeRequestPacket>(container);
            container.RegisterIncomingPacket<UpdateRangePacket>();
            container.RegisterPacketHandler<UpdateRangePacket, UpdateRangePacketHandler>();
            container.RegisterIncomingPacket<ExtendedCommandPacket>();
            container.RegisterPacketHandler<ExtendedCommandPacket, ExtendedCommandPacketHandler>();
            container.RegisterIncomingPacket<QueryPropertiesPacket>();
            container.RegisterPacketHandler<QueryPropertiesPacket, QueryPropertiesPacketHandler>();
            RegisterIgnoredPacket<AttackRequestPacket>(container);
            RegisterIgnoredPacket<TextCommandPacket>(container);
            RegisterIgnoredPacket<ResynchronizeRequestPacket>(container);
            RegisterIgnoredPacket<OpenChatWindowPacket>(container);
            RegisterIgnoredPacket<ClientTypePacket>(container);
            RegisterIgnoredPacket<PublicHouseContentPacket>(container);
            container.RegisterAsyncPacketHandler<DeleteCharacterPacket, DeleteCharacterPacketHandler>();
            container.RegisterAsyncPacketHandler<CreateCharacterPacket, CreateCharacterPacketHandler>();
            container.RegisterAsyncPacketHandler<CreateCharacterEnhancedPacket, CreateCharacterEnhancedPacketHandler>();

            container.Register<ILocalizationService, LocalizationService>(Reuse.Singleton);
            container.Register<INameService, NameService>(Reuse.Singleton);
            container.Register<IItemTemplateService, ItemTemplateService>(Reuse.Singleton);
            container.Register<IMobileTemplateService, MobileTemplateService>(Reuse.Singleton);
            container.Register<IContainerLayoutService, ContainerLayoutService>(Reuse.Singleton);
            container.Register<IItemFactoryService, ItemFactoryService>(Reuse.Singleton);
            container.Register<IDecorationsLoader, DecorationsLoader>(Reuse.Singleton);
            container.Register<IDecorationService, DecorationService>(Reuse.Singleton);
            container.Register<IClockService, ClockService>(Reuse.Singleton);
            container.Register<ILootService, LootService>(Reuse.Singleton);
            container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);
            container.Register<INpcTickService, NpcTickService>(Reuse.Singleton);
            // After the script engine (70) and its bootstrap: the mobile scripts load into the running engine.
            container.AddMoongateService<NpcScriptService>(LuaScriptEngineService.StartupPriority + 5);
            container.RegisterDelegate<INpcThinker>(resolver => resolver.Resolve<NpcScriptService>(), Reuse.Singleton);
            container.RegisterDelegate<INpcScriptService>(resolver => resolver.Resolve<NpcScriptService>(), Reuse.Singleton);
            container.Register<INpcSpeechListener, NpcHearingService>(Reuse.Singleton);
            container.Register<INpcSenseService, NpcSenseService>(Reuse.Singleton);
            // After the script engine (70), as the mobile scripts.
            container.AddMoongateService<IItemScriptService, ItemScriptService>(LuaScriptEngineService.StartupPriority + 5);
            container.AddMetricProvider<NpcTickMetricsProvider>();
            container.Register<ISectorService, SectorService>(Reuse.Singleton);
            container.Register<IMobileService, MobileService>(Reuse.Singleton);
            container.Register<IWorldViewService, WorldViewService>(Reuse.Singleton);
            container.Register<IWorldTransactionService, WorldTransactionService>(Reuse.Singleton);
            container.Register<ICharacterPresence, SessionCharacterPresence>(Reuse.Singleton);
            container.Register<ICharacterService, CharacterService>(Reuse.Singleton);
            container.AddScriptEvent<CharacterCreatedEvent>("character_created", CharacterScriptEvents.CharacterCreated);
            container.AddScriptEvent<CharacterDeletionRequestedEvent>(
                "character_deletion_requested",
                CharacterScriptEvents.CharacterDeletionRequested
            );
            container.AddScriptEvent<CharacterEnteredWorldEvent>(
                "character_entered_world",
                CharacterScriptEvents.CharacterEnteredWorld
            );
            container.AddScriptEvent<CharacterLeftWorldEvent>(
                "character_left_world",
                CharacterScriptEvents.CharacterLeftWorld
            );
            // Between the packet dispatcher (60), which closes the sessions, and the world save (40): stopping waits for
            // the leave saves before the final save and persistence shut down.
            container.AddMoongateService<CharacterLeaveWorldService>(50);
            container.RegisterMapping<ISessionClosedListener, CharacterLeaveWorldService>();
            container.RegisterMapping<ICharacterLeaveWorldService, CharacterLeaveWorldService>();
            container.Register<ITargetService, TargetService>(Reuse.Singleton);
            container.RegisterMapping<ISessionClosedListener, ITargetService>();
            container.Register<IBroadcastService, BroadcastService>(Reuse.Singleton);
            container.Register<ISpeechService, SpeechService>(Reuse.Singleton);
            container.RegisterCommand<ShutdownCommand>(
                "shutdown",
                "Shuts down this server gracefully, immediately or after a delay: shutdown [seconds].",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.Administrator,
                CommandMessages.ShutdownDescription
            );
            container.RegisterCommand<SaveCommand>(
                "save",
                "Saves the world and tells every player when it is done.",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.Administrator,
                CommandMessages.SaveDescription
            );
            container.RegisterCommand<BroadcastCommand>(
                "broadcast",
                "Sends a system message to every player in this world: broadcast <text>.",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.Administrator,
                CommandMessages.BroadcastDescription
            );
            container.RegisterCommand<SpawnCommand>(
                "spawn",
                "Spawns an NPC from a mobile template where you target: spawn <template>.",
                CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.SpawnDescription
            );
            container.RegisterCommand<RemoveCommand>(
                "remove",
                "Removes the NPC you target.",
                CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.RemoveDescription
            );
            container.RegisterCommand<FameCommand>(
                "fame",
                "Sets the fame (0 to 32000) of the character or NPC you target.",
                CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.FameDescription
            );
            container.RegisterCommand<KarmaCommand>(
                "karma",
                "Sets the karma (-32000 to 32000) of the character or NPC you target.",
                CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.KarmaDescription
            );
            container.RegisterCommand<DecorateCommand>(
                "decorate",
                "Places the world decoration: doors, signs, lights and furniture.",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.Administrator,
                CommandMessages.DecorateDescription
            );
            container.RegisterCommand<GlobalLightCommand>(
                "globallight",
                "Sets the light of every player (0 brightest, 31 darkest) or, without a level, goes back to the time of day.",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.GlobalLightDescription
            );
            container.RegisterCommand<WhereCommand>(
                "where",
                "Shows what you target: its serial, or the map and location of a spot.",
                CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.WhereDescription
            );
            container.RegisterCommand<CharacterCommand>(
                "character",
                "Pending character deletions: character pending [account-serial]; character restore <character-serial>.",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.GameMaster,
                CommandMessages.CharacterDescription
            );
            container.Register<ITileDataService, TileDataService>(Reuse.Singleton);
            container.Register<IMovementService, MovementService>(Reuse.Singleton);
            container.Register<ILineOfSightService, LineOfSightService>(Reuse.Singleton);
            container.AddScriptModule<DiceModule>();
            container.AddScriptModule<LocalizationModule>();
            container.AddScriptModule<NpcModule>();
            container.AddScriptModule<ItemModule>();
            container.AddScriptModule<WorldModule>();

            // After IUltimaDataService (-10): loaders read MUL/UOP files after Files.SetDirectory.
            container.AddLiveWorldMobiles();
            container.AddLiveWorldItems();

            container.AddMoongateService<IDataLoaderService, DataLoaderService>(-5);
            // After the loaders: the maps come from data/maps.toml.
            container.AddMoongateService<IMapService, MapService>(-4);
            // After IUltimaDataService: the multis come from the client directory.
            container.AddMoongateService<IMultiService, MultiService>(-4);
            // After the loaders and the item templates: checks the configured backpack and gold templates exist.
            container.AddMoongateService<IStartingItemsService, StartingItemsService>(-3);
            // After persistence is ready (it is, before any startup service): reserves item serials for the game loop.
            container.AddMoongateService<IItemSerialPool, ItemSerialPool>();
            // After the data loaders and the maps (the sector grid needs them), before the game server takes players and
            // the world save (40): the items on the ground are live before anyone can see them.
            container.AddMoongateService<IItemService, ItemService>(10);
            container.Register<IItemDecayQueue, ItemDecayQueue>(Reuse.Singleton);
            // After the items (10): the ground items it deletes are loaded by then.
            container.AddMoongateService<ItemDecayService>(11);
            // The light cycle's timer, like the decay's: the players it lights come after the game server starts.
            container.AddMoongateService<ILightService, LightService>(11);
            container.AddMoongateService<IEquipmentService, EquipmentService>();
            container.AddMoongateService<ITooltipService, TooltipService>();
            // As the ground items: the NPCs are live before the game server takes players.
            container.AddMoongateService<INpcService, NpcService>(10);
        }
    }

    private static void RegisterIgnoredPacket<TPacket>(Container container)
        where TPacket : class, IIncomingPacket<TPacket>
    {
        container.RegisterIncomingPacket<TPacket>();
        container.RegisterPacketHandler<TPacket, IgnoredPacketHandler<TPacket>>();
    }
}
