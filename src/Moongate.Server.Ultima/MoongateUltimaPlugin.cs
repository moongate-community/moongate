using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Scripting.Extensions.Scripts;
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

            container.RegisterPacketHandler<LoginSeedPacket, LoginSeedPacketHandler>();
            container.RegisterAsyncPacketHandler<GameLoginPacket, GameLoginPacketHandler>();
            container.RegisterIncomingPacket<ClientHardwareInfoPacket>();
            container.RegisterIncomingPacket<CreateCharacterPacket>();
            container.RegisterIncomingPacket<CreateCharacterEnhancedPacket>();

            container.Register<ILocalizationService, LocalizationService>(Reuse.Singleton);
            container.Register<INameService, NameService>(Reuse.Singleton);
            container.Register<ITileDataService, TileDataService>(Reuse.Singleton);
            container.Register<IMovementService, MovementService>(Reuse.Singleton);
            container.Register<ILineOfSightService, LineOfSightService>(Reuse.Singleton);
            container.AddScriptModule<LocalizationModule>();

            // After IUltimaDataService (-10): loaders read MUL/UOP files after Files.SetDirectory.
            container.AddPersistenceWorld<MobileEntity>();
            container.AddPersistenceWorld<ItemEntity>();

            container.AddMoongateService<IDataLoaderService, DataLoaderService>(-5);
            // After the loaders: the maps come from data/maps.toml.
            container.AddMoongateService<IMapService, MapService>(-4);
            // After IUltimaDataService: the multis come from the client directory.
            container.AddMoongateService<IMultiService, MultiService>(-4);
        }
    }
}
