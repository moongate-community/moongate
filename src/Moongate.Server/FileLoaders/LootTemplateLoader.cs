using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Loot;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads loot templates from <c>templates/loot</c> into <see cref="ILootTemplateService" />.
/// </summary>
[RegisterFileLoader(14)]
public sealed class LootTemplateLoader : IFileLoader
{
    private readonly ILogger _logger = Log.ForContext<LootTemplateLoader>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ILootTemplateService _lootTemplateService;
    private readonly Dictionary<string, string> _templateFileContents = new(StringComparer.OrdinalIgnoreCase);

    public LootTemplateLoader(
        DirectoriesConfig directoriesConfig,
        ILootTemplateService lootTemplateService
    )
    {
        _directoriesConfig = directoriesConfig;
        _lootTemplateService = lootTemplateService;
    }

    public Task LoadAsync()
    {
        var templatesRootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Templates], "loot");

        if (!Directory.Exists(templatesRootDirectory))
        {
            _logger.Warning("Loot templates directory not found: {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        var templateFiles = Directory.GetFiles(templatesRootDirectory, "*.json", SearchOption.AllDirectories);

        if (templateFiles.Length == 0)
        {
            _logger.Warning("No loot template files found in {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        _templateFileContents.Clear();

        foreach (var templateFile in templateFiles)
        {
            _templateFileContents[NormalizePath(templateFile)] = File.ReadAllText(templateFile);
        }

        var allLootTemplates = RebuildTemplatesFromCache();

        _lootTemplateService.Clear();

        _lootTemplateService.UpsertRange(allLootTemplates);

        _logger.Information(
            "Loaded {TemplateCount} loot templates from {FileCount} files",
            allLootTemplates.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }

    public Task LoadSingleAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = NormalizePath(filePath);
        _templateFileContents[normalizedPath] = File.ReadAllText(normalizedPath);
        var allLootTemplates = RebuildTemplatesFromCache();

        _lootTemplateService.Clear();
        _lootTemplateService.UpsertRange(allLootTemplates);

        _logger.Information("Reloaded loot template file {TemplateFile}", normalizedPath);

        return Task.CompletedTask;
    }

    private List<LootTemplateDefinition> RebuildTemplatesFromCache()
    {
        var allLootTemplates = new List<LootTemplateDefinition>();

        foreach (var (templateFile, json) in _templateFileContents.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            LootTemplateDefinitionBase[] templates;

            try
            {
                templates = Deserialize<LootTemplateDefinitionBase[]>(json);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load loot template file {TemplateFile}", templateFile);

                throw;
            }

            allLootTemplates.AddRange(templates.OfType<LootTemplateDefinition>());
        }

        return allLootTemplates;
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
