using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Extensions.Container;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Persistence.Extensions;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
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
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Handlers.Characters;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Handlers.Movement;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;

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
                CommandSourceType.Console | CommandSourceType.InGame
            );

            container.RegisterLoginPacketHandler<PingPacket, LoginRolePingPacketHandler>();
            container.RegisterLoginPacketHandler<LoginSeedPacket, LoginRoleSeedPacketHandler>();
            container.RegisterLoginPacketHandler<ClientVersionPacket, LoginRoleClientVersionPacketHandler>();
            container.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
            container.RegisterLoginPacketHandler<ServerSelectPacket, LoginRoleServerSelectPacketHandler>();
        }

        if ((mode & ServerMode.Game) != 0)
        {
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
            container.RegisterIncomingPacket<LiftRequestPacket>();
            container.RegisterPacketHandler<LiftRequestPacket, LiftRequestPacketHandler>();

            // Sent by the client around and after entering the world; recognised so it is not disconnected.
            RegisterIgnoredPacket<ClientHardwareInfoPacket>(container);
            RegisterIgnoredPacket<LookRequestPacket>(container);
            RegisterIgnoredPacket<MobileQueryPacket>(container);
            RegisterIgnoredPacket<WarModeRequestPacket>(container);
            RegisterIgnoredPacket<UpdateRangePacket>(container);
            RegisterIgnoredPacket<ExtendedCommandPacket>(container);
            RegisterIgnoredPacket<QueryPropertiesPacket>(container);
            RegisterIgnoredPacket<AttackRequestPacket>(container);
            RegisterIgnoredPacket<DropRequestPacket>(container);
            RegisterIgnoredPacket<TextCommandPacket>(container);
            RegisterIgnoredPacket<EquipRequestPacket>(container);
            RegisterIgnoredPacket<ResynchronizeRequestPacket>(container);
            RegisterIgnoredPacket<UnicodeSpeechRequestPacket>(container);
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
            container.Register<ILootService, LootService>(Reuse.Singleton);
            container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);
            container.Register<IMobileService, MobileService>(Reuse.Singleton);
            container.Register<IItemService, ItemService>(Reuse.Singleton);
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
            container.RegisterCommand<CharacterCommand>(
                "character",
                "Pending character deletions: character pending [account-serial]; character restore <character-serial>.",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.GameMaster
            );
            container.Register<ITileDataService, TileDataService>(Reuse.Singleton);
            container.Register<IMovementService, MovementService>(Reuse.Singleton);
            container.Register<ILineOfSightService, LineOfSightService>(Reuse.Singleton);
            container.AddScriptModule<DiceModule>();
            container.AddScriptModule<LocalizationModule>();

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
        }
    }

    private static void RegisterIgnoredPacket<TPacket>(Container container)
        where TPacket : class, IIncomingPacket<TPacket>
    {
        container.RegisterIncomingPacket<TPacket>();
        container.RegisterPacketHandler<TPacket, IgnoredPacketHandler<TPacket>>();
    }
}
