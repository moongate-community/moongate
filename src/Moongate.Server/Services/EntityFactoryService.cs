using System.Collections.Concurrent;
using Moongate.Core.Directories;
using Moongate.Core.Json;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Factory;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;
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
        DirectoriesConfig directoriesConfig,
        IItemService itemService,
        IMobileService mobileService,
        INameService nameService
    )
    {
        _directoriesConfig = directoriesConfig;
        _itemService = itemService;
        _mobileService = mobileService;
        _nameService = nameService;

        AddDefaultItems();

        AddDefaultMobiles();
    }



    public T CreateEntity<T>(string templateId) where T : class
        => throw new NotImplementedException();

    public T CreateEntity<T>(string templateId, Dictionary<string, object> overrides) where T : class
        => throw new NotImplementedException();

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

    public void Dispose() { }

    public UOItemEntity GetNewBackpack()
    {
        if (_itemTemplates.TryGetValue("backpack", out var backpackTemplate))
        {
            return CreateItemEntity(backpackTemplate);
        }

        _logger.Warning("Backpack template not found.");

        return null;
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

    public async Task StopAsync(CancellationToken cancellationToken = default) { }


    private void AddDefaultMobiles()
    {
        _mobileTemplates["chicken"] = new()
        {
            Id = "chicken",
            Name = "a chicken",
            Category = "Animals",
            Tags = ["animal", "bird"],
            Body = 0xD0,
            Strength = 5,
            Dexterity = 15,
            Intelligence = 5,
            Hits = 3,
            Mana = 0,
            Stamina = 15
        };

        _mobileTemplates["rabbit"] = new()
        {
            Id = "rabbit",
            Name = "a rabbit",
            Category = "Animals",
            Tags = ["animal", "small"],
            Body = 0xCD,
            Strength = 6,
            Dexterity = 26,
            Intelligence = 6,
            Hits = 4,
            Mana = 0,
            Stamina = 26
        };

        _mobileTemplates["deer"] = new()
        {
            Id = "deer",
            Name = "a deer",
            Category = "Animals",
            Tags = ["animal"],
            Body = 0xED,
            Strength = 21,
            Dexterity = 47,
            Intelligence = 14,
            Hits = 15,
            Mana = 0,
            Stamina = 47
        };

        _mobileTemplates["horse"] = new()
        {
            Id = "horse",
            Name = "a horse",
            Category = "Animals",
            Tags = ["animal", "mount"],
            Body = 0xC8,
            SkinHue = 0,
            Strength = 22,
            Dexterity = 56,
            Intelligence = 6,
            Hits = 15,
            Mana = 0,
            Stamina = 56
        };

        _mobileTemplates["cow"] = new()
        {
            Id = "cow",
            Name = "a cow",
            Category = "Animals",
            Tags = ["animal"],
            Body = 0xD8,
            Strength = 30,
            Dexterity = 15,
            Intelligence = 5,
            Hits = 18,
            Mana = 0,
            Stamina = 15
        };

        _mobileTemplates["pig"] = new()
        {
            Id = "pig",
            Name = "a pig",
            Category = "Animals",
            Tags = ["animal"],
            Body = 0xCB,
            Strength = 20,
            Dexterity = 20,
            Intelligence = 5,
            Hits = 12,
            Mana = 0,
            Stamina = 20
        };

        _mobileTemplates["cat"] = new()
        {
            Id = "cat",
            Name = "a cat",
            Category = "Animals",
            Tags = ["animal", "small"],
            Body = 0xC9,
            Strength = 9,
            Dexterity = 35,
            Intelligence = 5,
            Hits = 6,
            Mana = 0,
            Stamina = 35
        };

        _mobileTemplates["dog"] = new()
        {
            Id = "dog",
            Name = "a dog",
            Category = "Animals",
            Tags = ["animal", "small"],
            Body = 0xD9,
            Strength = 18,
            Dexterity = 25,
            Intelligence = 5,
            Hits = 12,
            Mana = 0,
            Stamina = 25
        };

        _mobileTemplates["sheep"] = new()
        {
            Id = "sheep",
            Name = "a sheep",
            Category = "Animals",
            Tags = ["animal"],
            Body = 0xCF,
            Strength = 19,
            Dexterity = 25,
            Intelligence = 5,
            Hits = 12,
            Mana = 0,
            Stamina = 25
        };

        _mobileTemplates["mongbat"] = new()
        {
            Id = "mongbat",
            Name = "a mongbat",
            Category = "Monsters",
            Tags = ["monster", "hostile"],
            Body = 0x27,
            Strength = 22,
            Dexterity = 43,
            Intelligence = 8,
            Hits = 14,
            Mana = 0,
            Stamina = 43
        };

        _mobileTemplates["skeleton"] = new()
        {
            Id = "skeleton",
            Name = "a skeleton",
            Category = "Undead",
            Tags = ["monster", "undead", "hostile"],
            Body = 0x32,
            Strength = 56,
            Dexterity = 31,
            Intelligence = 16,
            Hits = 34,
            Mana = 0,
            Stamina = 31
        };

        _mobileTemplates["zombie"] = new()
        {
            Id = "zombie",
            Name = "a zombie",
            Category = "Undead",
            Tags = ["monster", "undead", "hostile"],
            Body = 0x03,
            Strength = 46,
            Dexterity = 31,
            Intelligence = 16,
            Hits = 28,
            Mana = 0,
            Stamina = 46
        };

        _mobileTemplates["slime"] = new()
        {
            Id = "slime",
            Name = "a slime",
            Category = "Monsters",
            Tags = ["monster", "hostile"],
            Body = 0x33,
            SkinHue = 0x226,
            Strength = 22,
            Dexterity = 16,
            Intelligence = 16,
            Hits = 15,
            Mana = 0,
            Stamina = 16
        };

        _mobileTemplates["orc"] = new()
        {
            Id = "orc",
            Name = "an orc",
            Category = "Monsters",
            Tags = ["monster", "humanoid", "hostile"],
            Body = 0x11,
            Strength = 96,
            Dexterity = 81,
            Intelligence = 36,
            Hits = 58,
            Mana = 0,
            Stamina = 81
        };

        _mobileTemplates["ettin"] = new()
        {
            Id = "ettin",
            Name = "an ettin",
            Category = "Monsters",
            Tags = ["monster", "hostile"],
            Body = 0x02,
            Strength = 136,
            Dexterity = 36,
            Intelligence = 36,
            Hits = 82,
            Mana = 0,
            Stamina = 36
        };

        _mobileTemplates["troll"] = new()
        {
            Id = "troll",
            Name = "a troll",
            Category = "Monsters",
            Tags = ["monster", "hostile"],
            Body = 0x36,
            Strength = 176,
            Dexterity = 46,
            Intelligence = 46,
            Hits = 106,
            Mana = 0,
            Stamina = 46
        };

        _mobileTemplates["ogre"] = new()
        {
            Id = "ogre",
            Name = "an ogre",
            Category = "Monsters",
            Tags = ["monster", "hostile"],
            Body = 0x01,
            Strength = 166,
            Dexterity = 46,
            Intelligence = 30,
            Hits = 100,
            Mana = 0,
            Stamina = 46
        };

        _mobileTemplates["lizardman"] = new()
        {
            Id = "lizardman",
            Name = "a lizardman",
            Category = "Monsters",
            Tags = ["monster", "humanoid", "hostile"],
            Body = 0x21,
            Strength = 96,
            Dexterity = 66,
            Intelligence = 36,
            Hits = 58,
            Mana = 0,
            Stamina = 66
        };

        _mobileTemplates["ratman"] = new()
        {
            Id = "ratman",
            Name = "a ratman",
            Category = "Monsters",
            Tags = ["monster", "humanoid", "hostile"],
            Body = 0x2A,
            Strength = 96,
            Dexterity = 81,
            Intelligence = 36,
            Hits = 58,
            Mana = 0,
            Stamina = 81
        };

        _mobileTemplates["gazer"] = new()
        {
            Id = "gazer",
            Name = "a gazer",
            Category = "Monsters",
            Tags = ["monster", "hostile"],
            Body = 0x16,
            Strength = 96,
            Dexterity = 81,
            Intelligence = 96,
            Hits = 58,
            Mana = 96,
            Stamina = 81
        };

        _mobileTemplates["dragon"] = new()
        {
            Id = "dragon",
            Name = "a dragon",
            Category = "Monsters",
            Tags = ["monster", "hostile", "boss"],
            Body = 0x3B,
            Strength = 796,
            Dexterity = 86,
            Intelligence = 436,
            Hits = 478,
            Mana = 436,
            Stamina = 86
        };

        _mobileTemplates["daemon"] = new()
        {
            Id = "daemon",
            Name = "a daemon",
            Category = "Monsters",
            Tags = ["monster", "hostile", "boss"],
            Body = 0x09,
            Strength = 476,
            Dexterity = 76,
            Intelligence = 301,
            Hits = 286,
            Mana = 301,
            Stamina = 76
        };

        _mobileTemplates["lich"] = new()
        {
            Id = "lich",
            Name = "a lich",
            Category = "Undead",
            Tags = ["monster", "undead", "hostile", "boss"],
            Body = 0x18,
            Strength = 171,
            Dexterity = 126,
            Intelligence = 276,
            Hits = 103,
            Mana = 276,
            Stamina = 126
        };

        _mobileTemplates["healer"] = new()
        {
            Id = "healer",
            Name = "a healer",
            Category = "NPCs",
            Tags = ["npc", "vendor", "human"],
            Body = 0x190,
            SkinHue = 0x83EA,
            HairStyle = 0x203B,
            HairHue = 0x47E,
            Strength = 50,
            Dexterity = 50,
            Intelligence = 80,
            Hits = 50,
            Mana = 80,
            Stamina = 50,
            Brain = "Vendor"
        };

        _mobileTemplates["blacksmith"] = new()
        {
            Id = "blacksmith",
            Name = "a blacksmith",
            Category = "NPCs",
            Tags = ["npc", "vendor", "human"],
            Body = 0x190,
            SkinHue = 0x83EA,
            HairStyle = 0x203C,
            HairHue = 0x26C,
            Strength = 80,
            Dexterity = 50,
            Intelligence = 50,
            Hits = 80,
            Mana = 50,
            Stamina = 50,
            Brain = "Vendor"
        };

        _mobileTemplates["mage"] = new()
        {
            Id = "mage",
            Name = "a mage",
            Category = "NPCs",
            Tags = ["npc", "vendor", "human"],
            Body = 0x190,
            SkinHue = 0x83EA,
            HairStyle = 0x2048,
            HairHue = 0x455,
            Strength = 40,
            Dexterity = 50,
            Intelligence = 100,
            Hits = 40,
            Mana = 100,
            Stamina = 50,
            Brain = "Vendor"
        };

        _mobileTemplates["guard"] = new()
        {
            Id = "guard",
            Name = "a guard",
            Category = "NPCs",
            Tags = ["npc", "guard", "human", "invulnerable"],
            Body = 0x190,
            SkinHue = 0x83EA,
            HairStyle = 0x203C,
            HairHue = 0x26C,
            Strength = 1000,
            Dexterity = 150,
            Intelligence = 100,
            Hits = 1000,
            Mana = 100,
            Stamina = 150,
            Brain = "Guard"
        };
    }

    private void AddDefaultItems()
    {
        _itemTemplates["backpack"] = new()
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

        _itemTemplates["gold"] = new()
        {
            GumpId = 0x003C,
            Id = "gold",
            Category = "Currency",
            Tags = ["currency", "money"],
            ItemId = 0x0EEF,
            GoldValue = 1,
            Weight = 25
        };

        _itemTemplates["shirt"] = new()
        {
            Id = "shirt",
            Name = "Shirt",
            Category = "Clothing",
            Tags = ["clothing", "shirt"],
            ItemId = 0x1517,
            Weight = 1
        };

        _itemTemplates["pants"] = new()
        {
            Id = "pants",
            Name = "Pants",
            Category = "Clothing",
            Tags = ["clothing", "pants"],
            ItemId = 0x152E,
            Weight = 1
        };

        _itemTemplates["shoes"] = new()
        {
            Id = "shoes",
            Name = "Shoes",
            Category = "Clothing",
            Tags = ["clothing", "shoes"],
            ItemId = 0x170F,
            Weight = 1
        };
    }

    private UOItemEntity CreateItemEntity(ItemTemplate itemTemplate, Dictionary<string, object> overrides = null)
    {
        var item = _itemService.CreateItem();

        item.TemplateId = itemTemplate.Id;
        item.Gold = itemTemplate.GoldValue;

        item.Name = itemTemplate.Name;
        item.Amount = 1;
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
            var createdItems = new List<UOItemEntity>();

            foreach (var containerName in itemTemplate.Container)
            {
                var createdItem = CreateItemEntity(containerName, overrides);

                createdItems.Add(createdItem);
            }

            ContainerLayoutSystem.ArrangeItemsInGrid(item, createdItems);
        }

        _itemService.AddItem(item);

        return item;
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
}
