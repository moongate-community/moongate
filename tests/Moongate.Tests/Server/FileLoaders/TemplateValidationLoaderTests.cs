using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public class TemplateValidationLoaderTests
{
    [Test]
    public void LoadAsync_WhenAdditiveLootUsesWeight_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "gold",
                Name = "Gold",
                Category = "misc",
                Description = "gold",
                ItemId = "0x0EED",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 0.01M,
                Tags = ["currency"]
            }
        );

        lootTemplateService.Upsert(
            new()
            {
                Id = "undead.zombie",
                Name = "Zombie Loot",
                Category = "loot",
                Description = string.Empty,
                Mode = LootTemplateMode.Additive,
                Entries =
                [
                    new()
                    {
                        ItemTemplateId = "gold",
                        Weight = 10
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenBookTemplateReferencesMissingBookFile_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "welcome_book",
                Name = "Welcome Book",
                Category = "books",
                Description = "welcome",
                ItemId = "0x0FF0",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Tags = ["book"],
                BookId = "missing_book"
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenContainerItemHasMissingLayoutId_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "item.container",
                Name = "Container",
                Category = "container",
                Description = "container",
                ItemId = "0x0E76",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Tags = ["container"]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenItemReferencesMissingLootTable_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);
        ContainerLayoutSystem.ContainerSizesById["wooden_chest"] = new("wooden_chest", 7, 4, "Wooden Chest");

        itemService.Upsert(
            new()
            {
                Id = "item.loot_chest",
                Name = "Loot Chest",
                Category = "containers",
                Description = "loot chest",
                ItemId = "0x0E40",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "items.loot_chest",
                Weight = 1,
                Tags = ["container"],
                ContainerLayoutId = "wooden_chest",
                LootTables = ["missing_loot"]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingLootTable_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "loot_mobile",
                Name = "Loot Mobile",
                Category = "test",
                Description = "test",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                LootTables = ["missing_loot"]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingDefaultFaction_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "faction_guard",
                Name = "Faction Guard",
                Category = "guards",
                Description = "guard",
                Body = 0x11,
                SkinHue = HueSpec.FromValue(779),
                HairHue = HueSpec.FromValue(0),
                DefaultFactionId = "missing_faction"
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingItem_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "orc",
                Name = "Orc",
                Category = "monsters",
                Description = "orc",
                Body = 0x11,
                SkinHue = HueSpec.FromValue(779),
                HairHue = HueSpec.FromValue(0),
                FixedEquipment =
                [
                    new()
                    {
                        ItemTemplateId = "item.missing",
                        Layer = ItemLayerType.Shirt
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingSellProfile_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        mobileService.Upsert(
            new()
            {
                Id = "vendor_missing_profile",
                Name = "Vendor",
                Category = "vendors",
                Description = "vendor",
                Body = 0x11,
                SkinHue = HueSpec.FromValue(779),
                HairHue = HueSpec.FromValue(0),
                SellProfileId = "missing_profile"
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public async Task LoadAsync_WhenTemplatesAreValid_ShouldNotThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);
        ContainerLayoutSystem.ContainerSizesById["backpack"] = new("backpack", 7, 4, "Backpack");
        factionTemplateService.Upsert(
            new()
            {
                Id = "true_britannians",
                Name = "True Britannians"
            }
        );
        lootTemplateService.Upsert(
            new()
            {
                Id = "minor_treasure",
                Name = "Minor Treasure",
                Category = "loot",
                Description = "test",
                Entries =
                [
                    new()
                    {
                        ItemTemplateId = "item.shirt",
                        Weight = 1,
                        Amount = 1
                    }
                ]
            }
        );

        itemService.Upsert(
            new()
            {
                Id = "item.shirt",
                Name = "Shirt",
                Category = "clothes",
                Description = "shirt",
                ItemId = "0x1517",
                Hue = HueSpec.FromRange(5, 55),
                GoldValue = GoldValueSpec.FromDiceExpression("1d8+8"),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 1,
                Tags = ["container"],
                ContainerLayoutId = "backpack",
                LootTables = ["minor_treasure"]
            }
        );

        mobileService.Upsert(
            new()
            {
                Id = "orc",
                Name = "Orc",
                Category = "monsters",
                Description = "orc",
                Body = 0x11,
                SkinHue = HueSpec.FromValue(779),
                HairHue = HueSpec.FromValue(0),
                DefaultFactionId = "true_britannians",
                FixedEquipment =
                [
                    new()
                    {
                        ItemTemplateId = "item.shirt",
                        Layer = ItemLayerType.Shirt
                    }
                ]
            }
        );

        sellProfileService.Upsert(
            new()
            {
                Id = "basic_vendor",
                Name = "Basic Vendor",
                Category = "vendor",
                Description = "basic",
                VendorItems =
                [
                    new()
                    {
                        ItemTemplateId = "item.shirt",
                        Price = 50
                    }
                ]
            }
        );

        mobileService.Upsert(
            new()
            {
                Id = "vendor_orc",
                Name = "Vendor Orc",
                Category = "vendors",
                Description = "vendor",
                Body = 0x11,
                SkinHue = HueSpec.FromValue(779),
                HairHue = HueSpec.FromValue(0),
                SellProfileId = "basic_vendor"
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.That(async () => await loader.LoadAsync(), Throws.Nothing);
    }

    [Test]
    public void LoadAsync_WhenWeightedLootUsesAmountRange_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var factionTemplateService = new FactionTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        var lootTemplateService = new LootTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);

        itemService.Upsert(
            new()
            {
                Id = "gold",
                Name = "Gold",
                Category = "misc",
                Description = "gold",
                ItemId = "0x0EED",
                Hue = HueSpec.FromValue(0),
                GoldValue = GoldValueSpec.FromValue(0),
                LootType = LootType.Regular,
                ScriptId = "none",
                Weight = 0.01M,
                Tags = ["currency"]
            }
        );

        lootTemplateService.Upsert(
            new()
            {
                Id = "treasure.small",
                Name = "Treasure Small",
                Category = "loot",
                Description = string.Empty,
                Mode = LootTemplateMode.Weighted,
                Entries =
                [
                    new()
                    {
                        ItemTemplateId = "gold",
                        Weight = 1,
                        AmountMin = 20,
                        AmountMax = 40
                    }
                ]
            }
        );

        var loader = new TemplateValidationLoader(
            itemService,
            mobileService,
            factionTemplateService,
            sellProfileService,
            bookTemplateService,
            lootTemplateService
        );

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [SetUp]
    public void SetUp()
    {
        ContainerLayoutSystem.ContainerSizes.Clear();
        ContainerLayoutSystem.ContainerSizesById.Clear();
    }

    private static BookTemplateService CreateBookTemplateService(string rootPath)
    {
        var directoriesConfig = new DirectoriesConfig(
            rootPath,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        return new(directoriesConfig, new());
    }
}
