using System.Collections.Concurrent;
using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Factory;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

public class EntityFactoryService : IEntityFactoryService
{
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ILogger _logger = Log.ForContext<EntityFactoryService>();

    private readonly ConcurrentDictionary<string, ItemTemplate> _itemTemplates = new();
    private readonly ConcurrentDictionary<string, MobileTemplate> _mobileTemplates = new();

    private readonly IItemService _itemService;

    public EntityFactoryService(DirectoriesConfig directoriesConfig, IItemService itemService)
    {
        _directoriesConfig = directoriesConfig;
        _itemService = itemService;

        AddDefaultItems();
    }

    private void AddDefaultItems()
    {
        _itemTemplates["backpack"] = new ItemTemplate()
        {
            GumpId = 0x003C,
            Id = "backpack",
            Name = "Backpack",
            Category = "Containers",
            Tags = ["container", "bag"],
            ItemId = 0x1F9E,
            GoldValue = 1,
            Weight = 1
        };
    }

    public T CreateEntity<T>(string templateId) where T : class
    {
        throw new NotImplementedException();
    }

    public T CreateEntity<T>(string templateId, Dictionary<string, object> overrides) where T : class
    {
        throw new NotImplementedException();
    }

    public UOItemEntity CreateItemEntity(string templateOrCategoryOrTag, Dictionary<string, object> overrides = null)
    {
        if (_itemTemplates.TryGetValue(templateOrCategoryOrTag, out var itemTemplate))
        {
            return CreateItemEntity(itemTemplate, overrides);
        }

        // If not found by template ID, try category or tag
        foreach (var kvp in _itemTemplates)
        {
            if (kvp.Value.Category == templateOrCategoryOrTag || kvp.Value.Tags.Contains(templateOrCategoryOrTag))
            {
                return CreateItemEntity(kvp.Value, overrides);
            }
        }

        _logger.Warning("Item template not found: {TemplateId}", templateOrCategoryOrTag);
        return null;
    }

    private UOItemEntity CreateItemEntity(ItemTemplate itemTemplate, Dictionary<string, object> overrides = null)
    {
        var item = _itemService.CreateItem();

        item.TemplateId = itemTemplate.Id;
        item.Gold = itemTemplate.GoldValue;
        item.Name = itemTemplate.Name;
        item.ItemId = itemTemplate.ItemId;
        item.Weight = itemTemplate.Weight;
        item.Hue = itemTemplate.Hue;
        item.ScriptId = itemTemplate.ScriptId;
        item.GumpId = itemTemplate.GumpId;

        if (itemTemplate.Container.Count > 0)
        {
            var startingPosition = new Point2D(0, 0);

            foreach (var containerName in itemTemplate.Container)
            {
                item.AddItem(CreateItemEntity(containerName, overrides), startingPosition);
            }
        }

        _itemService.AddItem(item);

        return item;
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
                    if (itemTemplate.Name == null)
                    {
                        itemTemplate.Name = TileData.ItemTable[itemTemplate.ItemId].Name;
                    }

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

    public UOItemEntity GetBackpack()
    {
        if (_itemTemplates.TryGetValue("backpack", out var backpackTemplate))
        {
            return CreateItemEntity(backpackTemplate);
        }

        _logger.Warning("Backpack template not found.");
        return null;
    }

    public void Dispose()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var templates = Directory.GetFiles(
            _directoriesConfig[DirectoryType.Templates],
            "*.json",
            SearchOption.AllDirectories
        );


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
