using System.Collections.Concurrent;
using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Factory;
using Serilog;

namespace Moongate.Server.Services;

public class EntityFactoryService : IEntityFactoryService
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ILogger _logger = Log.ForContext<EntityFactoryService>();

    private readonly ConcurrentDictionary<string, ItemTemplate> _itemTemplates = new();
    private readonly ConcurrentDictionary<string, MobileTemplate> _mobileTemplates = new();


    public EntityFactoryService(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public T CreateEntity<T>(string templateId) where T : class
    {
        throw new NotImplementedException();
    }

    public T CreateEntity<T>(string templateId, Dictionary<string, object> overrides) where T : class
    {
        throw new NotImplementedException();
    }

    public async Task LoadTemplatesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warning("Templates file not found: {FilePath}", filePath);
            return;
        }

        var templates = JsonUtils.DeserializeFromFile<BaseTemplate[]>(filePath);

        foreach (var template in templates)
        {
            if (template is ItemTemplate itemTemplate)
            {
                if (_itemTemplates.TryAdd(itemTemplate.Id, itemTemplate))
                {
                    _logger.Information("Loaded item template: {TemplateId}", itemTemplate.Id);
                }
            }

            if (template is MobileTemplate mobileTemplate)
            {
                if (_mobileTemplates.TryAdd(mobileTemplate.Id, mobileTemplate))
                {
                    _logger.Information("Loaded mobile template: {TemplateId}", mobileTemplate.Id);
                }
                else
                {
                    _logger.Warning("Duplicate mobile template found: {TemplateId}", mobileTemplate.Id);
                }
            }
        }

        _logger.Information("Loaded {Count} templates from {FilePath}", templates.Length, filePath);
    }

    public void Dispose()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var templates = Directory.GetFiles(_directoriesConfig[DirectoryType.Templates], "*.json", SearchOption.AllDirectories);


        var loadTasks = templates.Select(LoadTemplatesAsync);

        try
        {
            await Task.WhenAll(loadTasks);
            _logger.Information("All templates loaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading templates.");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
    }
}
