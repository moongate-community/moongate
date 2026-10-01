using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Extensions;

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
        CreateDirectories(container.Resolve<DirectoriesConfig>());
        RegisterTomlConverters();

        var mode = container.IsRegistered<ServerMode>() ? container.Resolve<ServerMode>() : ServerMode.Standalone;

        if ((mode & ServerMode.Login) != 0)
        {
            container.AddUltimaLoginRole();
        }

        if ((mode & ServerMode.Game) != 0)
        {
            container.AddUltimaMotd()
                .AddUltimaDataLoaders()
                .AddUltimaGamePackets()
                .AddUltimaWorldServices()
                .AddUltimaCommands();
        }
    }

    private static void CreateDirectories(DirectoriesConfig directoriesConfig)
    {
        // Every folder the loaders and the script services read, so a new root starts with its layout.
        directoriesConfig.CreateDirectoryIfNotExists("data/");
        directoriesConfig.CreateDirectoryIfNotExists("data/messages/");
        directoriesConfig.CreateDirectoryIfNotExists("data/regions/");
        directoriesConfig.CreateDirectoryIfNotExists("templates");
        directoriesConfig.CreateDirectoryIfNotExists("templates/mobiles/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/items/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/loots/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/decorations/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/npc_lists/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/spawns/");
        directoriesConfig.CreateDirectoryIfNotExists("templates/gumps/");
        directoriesConfig.CreateDirectoryIfNotExists("scripts/items/");
        directoriesConfig.CreateDirectoryIfNotExists("scripts/mobiles/");
        directoriesConfig.CreateDirectoryIfNotExists("scripts/gumps/");
    }

    private static void RegisterTomlConverters()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new Point2DTomlConverter());
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new Rectangle2DTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }
}
