using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Factions;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads faction templates from <c>templates/factions</c> into <see cref="IFactionTemplateService" />.
/// </summary>
[RegisterFileLoader(14)]
public sealed class FactionTemplateLoader : IFileLoader
{
    private readonly ILogger _logger = Log.ForContext<FactionTemplateLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IFactionTemplateService _factionTemplateService;

    public FactionTemplateLoader(
        DirectoriesConfig directoriesConfig,
        IFactionTemplateService factionTemplateService
    )
    {
        _directoriesConfig = directoriesConfig;
        _factionTemplateService = factionTemplateService;
    }

    public Task LoadAsync()
    {
        var templatesRootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Templates], "factions");

        if (!Directory.Exists(templatesRootDirectory))
        {
            _logger.Warning("Faction templates directory not found: {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        var templateFiles = Directory.GetFiles(templatesRootDirectory, "*.json", SearchOption.AllDirectories);

        if (templateFiles.Length == 0)
        {
            _logger.Warning("No faction template files found in {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        _factionTemplateService.Clear();
        var allFactionTemplates = new List<FactionDefinition>();

        foreach (var templateFile in templateFiles)
        {
            FactionDefinitionBase[] templates;

            try
            {
                templates = JsonUtils.DeserializeFromFile<FactionDefinitionBase[]>(
                    templateFile,
                    MoongateUOTemplateJsonContext.Default
                );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load faction template file {TemplateFile}", templateFile);

                throw;
            }

            allFactionTemplates.AddRange(templates.OfType<FactionDefinition>());
        }

        _factionTemplateService.UpsertRange(allFactionTemplates);

        _logger.Information(
            "Loaded {TemplateCount} faction templates from {FileCount} files",
            allFactionTemplates.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }
}
