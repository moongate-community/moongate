using System.Collections.Concurrent;
using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Factory;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
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
    private readonly INameService _nameService;
    private readonly IMobileService _mobileService;

    public EntityFactoryService(
        DirectoriesConfig directoriesConfig, IItemService itemService, IMobileService mobileService, INameService nameService
    )
    {
        _directoriesConfig = directoriesConfig;
        _itemService = itemService;
        _mobileService = mobileService;
        _nameService = nameService;

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

        _itemTemplates["gold"] = new ItemTemplate()
        {
            GumpId = 0x003C,
            Id = "gold",
            Category = "Currency",
            Tags = ["currency", "money"],
            ItemId = 0x0EEF,
            GoldValue = 1,
            Weight = 25
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

    public UOMobileEntity CreateMobileEntity(string templateOrCategoryOrTag, Dictionary<string, object> overrides = null)
    {
        if (_mobileTemplates.TryGetValue(templateOrCategoryOrTag, out var mobileTemplate))
        {
            return CreateMobileEntity(mobileTemplate, overrides);
        }

        // If not found by template ID, try category or tag
        foreach (var kvp in _mobileTemplates)
        {
            if (kvp.Value.Category == templateOrCategoryOrTag || kvp.Value.Tags.Contains(templateOrCategoryOrTag))
            {
                return CreateMobileEntity(kvp.Value, overrides);
            }
        }

        _logger.Warning("Mobile template not found: {TemplateId}", templateOrCategoryOrTag);
        return null;
    }

    private UOMobileEntity CreateMobileEntity(MobileTemplate mobileTemplate, Dictionary<string, object> overrides = null)
    {
        var mobile = _mobileService.CreateMobile();
        mobile.TemplateId = mobileTemplate.Id;

        mobile.Name = mobileTemplate.Name ?? _nameService.GenerateName(mobileTemplate);

        mobile.Body = mobileTemplate.Body;

        mobile.HairStyle = mobileTemplate.HairStyle;
        mobile.HairHue = mobileTemplate.HairHue;

        mobile.BrainId = mobileTemplate.Brain;

        mobile.FacialHairStyle = 0;

        mobile.FacialHairStyle = 0;

        mobile.Equipment[ItemLayerType.Backpack] = CreateItemEntity("backpack").ToItemReference();

        return mobile;
    }

    private UOItemEntity CreateItemEntity(ItemTemplate itemTemplate, Dictionary<string, object> overrides = null)
    {
        var item = _itemService.CreateItem();

        item.TemplateId = itemTemplate.Id;
        item.Gold = itemTemplate.GoldValue;

        item.Name = itemTemplate.Name;

        item.ItemId = itemTemplate.ItemId;
        item.BaseWeight = itemTemplate.Weight;
        item.Hue = itemTemplate.Hue;
        item.ScriptId = itemTemplate.ScriptId;
        item.GumpId = itemTemplate.GumpId;
        item.LootType = itemTemplate.LootType;
        item.IsMovable = itemTemplate.IsMovable;

        item.Name ??= TileData.ItemTable[item.ItemId].Name;

        if (itemTemplate.Container.Count > 0)
        {
            //var startingPosition = new Point2D(0, 0);

            //foreach (var containerName in itemTemplate.Container)
            //{
            //    item.AddItem(CreateItemEntity(containerName, overrides), startingPosition);
            //}

            var itemsToAdd = itemTemplate.Container
                .Select(containerName => CreateItemEntity(containerName, overrides))
                .Where(createdItem => createdItem != null)
                .ToList();

            ContainerLayoutSystem.ArrangeItemsInGrid(item, itemsToAdd);
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


        try
        {
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

        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading templates from file: {FilePath}", filePath);
            throw;
        }
    }

    public UOItemEntity GetNewBackpack()
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
            _logger.Information(
                "All templates loaded successfully. Loaded {Items} and {Mobiles}",
                _itemTemplates.Count,
                _mobileTemplates.Count
            );
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
