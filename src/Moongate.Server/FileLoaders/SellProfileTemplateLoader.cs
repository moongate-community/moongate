using System.Text.Json;
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
    private readonly Dictionary<string, string> _templateFileContents = new(StringComparer.OrdinalIgnoreCase);

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

        _templateFileContents.Clear();

        foreach (var templateFile in templateFiles)
        {
            _templateFileContents[NormalizePath(templateFile)] = File.ReadAllText(templateFile);
        }

        var allProfiles = RebuildProfilesFromCache();

        _sellProfileTemplateService.UpsertRange(allProfiles);

        _logger.Information(
            "Loaded {ProfileCount} sell profile templates from {FileCount} files",
            allProfiles.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }

    public Task LoadSingleAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = NormalizePath(filePath);
        _templateFileContents[normalizedPath] = File.ReadAllText(normalizedPath);
        var allProfiles = RebuildProfilesFromCache();

        _sellProfileTemplateService.Clear();
        _sellProfileTemplateService.UpsertRange(allProfiles);

        _logger.Information("Reloaded sell profile template file {TemplateFile}", normalizedPath);

        return Task.CompletedTask;
    }

    private List<SellProfileTemplateDefinition> RebuildProfilesFromCache()
    {
        var allProfiles = new List<SellProfileTemplateDefinition>();

        foreach (var (templateFile, json) in _templateFileContents.OrderBy(static entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            SellProfileTemplateDefinitionBase[] templates;

            try
            {
                templates = Deserialize<SellProfileTemplateDefinitionBase[]>(json);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load sell profile template file {TemplateFile}", templateFile);

                throw;
            }

            allProfiles.AddRange(templates.OfType<SellProfileTemplateDefinition>());
        }

        return allProfiles;
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
