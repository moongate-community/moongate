using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.SellProfiles;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads sell profile templates from <c>templates/sell_profiles</c> into <see cref="ISellProfileTemplateService" />.
/// </summary>
[RegisterFileLoader(14)]
public sealed class SellProfileTemplateLoader : IFileLoader
{
    private readonly ILogger _logger = Log.ForContext<SellProfileTemplateLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ISellProfileTemplateService _sellProfileTemplateService;

    public SellProfileTemplateLoader(
        DirectoriesConfig directoriesConfig,
        ISellProfileTemplateService sellProfileTemplateService
    )
    {
        _directoriesConfig = directoriesConfig;
        _sellProfileTemplateService = sellProfileTemplateService;
    }

    public Task LoadAsync()
    {
        var templatesRootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Templates], "sell_profiles");

        if (!Directory.Exists(templatesRootDirectory))
        {
            _logger.Warning("Sell profile templates directory not found: {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        var templateFiles = Directory.GetFiles(templatesRootDirectory, "*.json", SearchOption.AllDirectories);

        if (templateFiles.Length == 0)
        {
            _logger.Warning("No sell profile template files found in {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        _sellProfileTemplateService.Clear();
        var allProfiles = new List<SellProfileTemplateDefinition>();

        foreach (var templateFile in templateFiles)
        {
            SellProfileTemplateDefinitionBase[] templates;

            try
            {
                templates = JsonUtils.DeserializeFromFile<SellProfileTemplateDefinitionBase[]>(
                    templateFile,
                    MoongateUOTemplateJsonContext.Default
                );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load sell profile template file {TemplateFile}", templateFile);

                throw;
            }

            allProfiles.AddRange(templates.OfType<SellProfileTemplateDefinition>());
        }

        _sellProfileTemplateService.UpsertRange(allProfiles);

        _logger.Information(
            "Loaded {ProfileCount} sell profile templates from {FileCount} files",
            allProfiles.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }
}
