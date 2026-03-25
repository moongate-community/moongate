using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.FileLoaders;
using Moongate.Server.Json;
using Moongate.Server.Services.Scripting;
using Moongate.TemplateValidator.Data.Internal;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.TemplateValidator.Services;

public sealed class TemplateValidatorCompositionRoot
{
    internal TemplateValidatorRuntimeContext Create(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var resolvedRootDirectory = TemplateValidatorRootDirectoryResolver.Resolve(rootDirectory);

        if (!Directory.Exists(resolvedRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Root directory '{resolvedRootDirectory}' does not exist."
            );
        }

        var directoriesConfig = new DirectoriesConfig(
            resolvedRootDirectory,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );
        var config = LoadConfig(resolvedRootDirectory);

        var itemTemplateService = new ItemTemplateService();
        var mobileTemplateService = new MobileTemplateService();
        var lootTemplateService = new LootTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileTemplateService = new SellProfileTemplateService();
        var bookTemplateService = new BookTemplateService(directoriesConfig, config);

        var containersDataLoader = new ContainersDataLoader(directoriesConfig);
        var itemTemplateLoader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);
        var mobileTemplateLoader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);
        var lootTemplateLoader = new LootTemplateLoader(directoriesConfig, lootTemplateService);
        var factionTemplateLoader = new FactionTemplateLoader(directoriesConfig, factionTemplateService);
        var sellProfileTemplateLoader = new SellProfileTemplateLoader(directoriesConfig, sellProfileTemplateService);
        var templateValidationLoader = new TemplateValidationLoader(
            itemTemplateService,
            mobileTemplateService,
            factionTemplateService,
            sellProfileTemplateService,
            bookTemplateService,
            lootTemplateService
        );

        return new(
            directoriesConfig,
            config,
            itemTemplateService,
            mobileTemplateService,
            lootTemplateService,
            factionTemplateService,
            sellProfileTemplateService,
            bookTemplateService,
            containersDataLoader,
            itemTemplateLoader,
            mobileTemplateLoader,
            lootTemplateLoader,
            factionTemplateLoader,
            sellProfileTemplateLoader,
            templateValidationLoader
        );
    }

    private static MoongateConfig LoadConfig(string rootDirectory)
    {
        var configPath = Path.Combine(rootDirectory, "moongate.json");
        var config = File.Exists(configPath)
                         ? JsonUtils.DeserializeFromFile<MoongateConfig>(configPath, MoongateServerJsonContext.Default)
                         : new MoongateConfig();

        config.RootDirectory = rootDirectory;

        return config;
    }
}
