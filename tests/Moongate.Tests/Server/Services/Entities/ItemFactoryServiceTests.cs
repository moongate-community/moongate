using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Entities;

public class ItemFactoryServiceTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
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
                Weight = 6
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
                Assert.That(item.ScriptId, Is.EqualTo("items.test_item"));
                Assert.That(item.Location, Is.EqualTo(Point3D.Zero));
                Assert.That(item.ParentContainerId, Is.EqualTo(Moongate.UO.Data.Ids.Serial.Zero));
                Assert.That(item.EquippedLayer, Is.Null);
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
