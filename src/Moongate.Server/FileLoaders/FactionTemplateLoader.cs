using System.Text.Json;
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
    private readonly Dictionary<string, string> _templateFileContents = new(StringComparer.OrdinalIgnoreCase);

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

        _templateFileContents.Clear();

        foreach (var templateFile in templateFiles)
        {
            _templateFileContents[NormalizePath(templateFile)] = File.ReadAllText(templateFile);
        }

        var allFactionTemplates = RebuildTemplatesFromCache();

        _factionTemplateService.Clear();

        _factionTemplateService.UpsertRange(allFactionTemplates);

        _logger.Information(
            "Loaded {TemplateCount} faction templates from {FileCount} files",
            allFactionTemplates.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }

    public Task LoadSingleAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = NormalizePath(filePath);
        _templateFileContents[normalizedPath] = File.ReadAllText(normalizedPath);
        var allFactionTemplates = RebuildTemplatesFromCache();

        _factionTemplateService.Clear();
        _factionTemplateService.UpsertRange(allFactionTemplates);

        _logger.Information("Reloaded faction template file {TemplateFile}", normalizedPath);

        return Task.CompletedTask;
    }

    private List<FactionDefinition> RebuildTemplatesFromCache()
    {
        var allFactionTemplates = new List<FactionDefinition>();

        foreach (var (templateFile, json) in _templateFileContents.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            FactionDefinitionBase[] templates;

            try
            {
                templates = Deserialize<FactionDefinitionBase[]>(json);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load faction template file {TemplateFile}", templateFile);

                throw;
            }

            allFactionTemplates.AddRange(templates.OfType<FactionDefinition>());
        }

        return allFactionTemplates;
    }

    private static T Deserialize<T>(string json)
    {
        var result = JsonSerializer.Deserialize(json, MoongateUOTemplateJsonContext.Default.GetTypeInfo(typeof(T)));

        return result is T typedResult
                   ? typedResult
                   : throw new JsonException($"Deserialization returned null for type {typeof(T).Name}");
    }

    private static string NormalizePath(string filePath)
        => Path.GetFullPath(filePath);
}
