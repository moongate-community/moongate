using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Entities;

public class ItemFactoryServiceTests
{
    [Test]
    public async Task CreateItemFromTemplate_ShouldApplyTypedParamsToCustomProperties()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "param_item",
                Name = "Param Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.param_item",
                Weight = 1,
                Params = new()
                {
                    ["label"] = new() { Type = ItemTemplateParamType.String, Value = "hello" },
                    ["linked_id"] = new() { Type = ItemTemplateParamType.Serial, Value = "0x40000010" },
                    ["tint"] = new() { Type = ItemTemplateParamType.Hue, Value = "0x044D" }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("param_item");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.TryGetCustomString("label", out var label), Is.True);
                Assert.That(label, Is.EqualTo("hello"));
                Assert.That(item.TryGetCustomInteger("linked_id", out var linkedId), Is.True);
                Assert.That(linkedId, Is.EqualTo(0x40000010));
                Assert.That(item.TryGetCustomInteger("tint", out var tint), Is.True);
                Assert.That(tint, Is.EqualTo(0x044D));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldFallbackToTileNameAndWeight_WhenTemplateUsesDefaults()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "fallback_item",
                Name = string.Empty,
                Category = "test",
                Description = "test",
                ItemId = "0x0E75",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.fallback_item",
                Weight = 0
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("fallback_item");
        var tile = TileData.ItemTable[item.ItemId];

        Assert.Multiple(
            () =>
            {
                Assert.That(item.Name, Is.EqualTo(tile.Name));
                Assert.That(item.Weight, Is.EqualTo(tile.Weight));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldMapTemplateFieldsAndAllocateItemSerial()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "test_item",
                Name = "Test Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                GumpId = "0x0042",
                Hue = HueSpec.FromValue(77),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.test_item",
                Weight = 6,
                Rarity = ItemRarity.Legendary
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("test_item");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.Id.IsItem, Is.True);
                Assert.That(item.Name, Is.EqualTo("Test Item"));
                Assert.That(item.ItemId, Is.EqualTo(0x1517));
                Assert.That(item.GumpId, Is.EqualTo(0x0042));
                Assert.That(item.Hue, Is.EqualTo(77));
                Assert.That(item.Weight, Is.EqualTo(6));
                Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Legendary));
                Assert.That(item.Visibility, Is.EqualTo(AccountType.Regular));
                Assert.That(item.ScriptId, Is.EqualTo("items.test_item"));
                Assert.That(item.Location, Is.EqualTo(Point3D.Zero));
                Assert.That(item.ParentContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(item.EquippedLayer, Is.Null);
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldMapVisibilityFromTemplate()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "gm_only_item",
                Name = "GM Only Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.gm_only_item",
                Weight = 1,
                Visibility = AccountType.GameMaster
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("gm_only_item");

        Assert.That(item.Visibility, Is.EqualTo(AccountType.GameMaster));
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldResolveSnakeCaseFallback_FromPascalCase()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "barred_metal_door",
                Name = "Barred Metal Door",
                Category = "structure",
                Description = "test",
                ItemId = "0x0685",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.door",
                Weight = 5
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("BarredMetalDoor");

        Assert.Multiple(
            () =>
            {
                Assert.That(item.Name, Is.EqualTo("Barred Metal Door"));
                Assert.That(item.ItemId, Is.EqualTo(0x0685));
            }
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldSetStackableFromTileFlags()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "gold_item",
                Name = "Gold",
                Category = "test",
                Description = "test",
                ItemId = "0x0EED",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.gold_item",
                Weight = 1
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var item = service.CreateItemFromTemplate("gold_item");
        var expected = TileData.ItemTable[item.ItemId][UOTileFlag.Generic];

        Assert.That(item.IsStackable, Is.EqualTo(expected));
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldThrow_WhenSerialParamIsInvalid()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "invalid_serial_param_item",
                Name = "Invalid Serial Param Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.invalid_serial_param_item",
                Weight = 1,
                Params = new()
                {
                    ["linked_id"] = new() { Type = ItemTemplateParamType.Serial, Value = "not_a_serial" }
                }
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        Assert.That(
            () => service.CreateItemFromTemplate("invalid_serial_param_item"),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("invalid serial param")
        );
    }

    [TestCase(null), TestCase(""), TestCase(" ")]
    public async Task CreateItemFromTemplate_ShouldThrow_WhenTemplateIdIsInvalid(string? templateId)
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        Assert.That(
            () => service.CreateItemFromTemplate(templateId!),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public async Task CreateItemFromTemplate_ShouldThrow_WhenTemplateIsMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        Assert.That(
            () => service.CreateItemFromTemplate("missing_template"),
            Throws.TypeOf<InvalidOperationException>()
        );
    }

    [Test]
    public async Task GetNewBackpack_ShouldUseFallback_WhenTemplateIsMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        var backpack = service.GetNewBackpack();

        Assert.Multiple(
            () =>
            {
                Assert.That(backpack.Id.IsItem, Is.True);
                Assert.That(backpack.Name, Is.EqualTo("Backpack"));
                Assert.That(backpack.ItemId, Is.EqualTo(0x0E75));
                Assert.That(backpack.Hue, Is.EqualTo(0));
                Assert.That(backpack.ScriptId, Is.EqualTo("none"));
                Assert.That(backpack.IsStackable, Is.False);
            }
        );
    }

    [Test]
    public async Task GetNewBackpack_ShouldUseTemplate_WhenAvailable()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "backpack",
                Name = "Template Backpack",
                Category = "containers",
                Description = "Backpack",
                ItemId = "0x0E75",
                Hue = HueSpec.FromValue(33),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.backpack",
                Weight = 2
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var backpack = service.GetNewBackpack();

        Assert.Multiple(
            () =>
            {
                Assert.That(backpack.Id.IsItem, Is.True);
                Assert.That(backpack.Name, Is.EqualTo("Template Backpack"));
                Assert.That(backpack.ItemId, Is.EqualTo(0x0E75));
                Assert.That(backpack.Hue, Is.EqualTo(33));
                Assert.That(backpack.Weight, Is.EqualTo(2));
                Assert.That(backpack.ScriptId, Is.EqualTo("items.backpack"));
            }
        );
    }

    [Test]
    public async Task TryGetItemTemplate_ShouldResolveSnakeCaseFallback_FromPascalCase()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "barred_metal_door",
                Name = "Barred Metal Door",
                Category = "structure",
                Description = "test",
                ItemId = "0x0685"
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var found = service.TryGetItemTemplate("BarredMetalDoor", out var template);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(template, Is.Not.Null);
                Assert.That(template!.Id, Is.EqualTo("barred_metal_door"));
            }
        );
    }

    [Test]
    public async Task TryGetItemTemplate_ShouldReturnFalse_WhenTemplateMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        var service = new ItemFactoryService(templateService, persistence);

        var found = service.TryGetItemTemplate("missing_template", out var template);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.False);
                Assert.That(template, Is.Null);
            }
        );
    }

    [Test]
    public async Task TryGetItemTemplate_ShouldReturnTrue_WhenTemplateExists()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new ItemTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "test_item",
                Name = "Test Item",
                Category = "test",
                Description = "test",
                ItemId = "0x1517"
            }
        );

        var service = new ItemFactoryService(templateService, persistence);

        var found = service.TryGetItemTemplate("test_item", out var template);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(template, Is.Not.Null);
                Assert.That(template!.Id, Is.EqualTo("test_item"));
            }
        );
    }

    private static async Task<PersistenceService> CreatePersistenceServiceAsync(string rootDirectory)
    {
        var directories = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());
        var persistence = new PersistenceService(
            directories,
            new TimerWheelService(
                new()
                {
                    TickDuration = TimeSpan.FromMilliseconds(250),
                    WheelSize = 512
                }
            ),
            new(),
            new NetworkServiceTestGameEventBusService()
        );

        await persistence.StartAsync();

        return persistence;
    }
}
