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
    public void LoadAsync_WhenBookTemplateReferencesMissingBookFile_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var sellProfileService = new SellProfileTemplateService();
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

        var loader = new TemplateValidationLoader(itemService, mobileService, sellProfileService, bookTemplateService);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenContainerItemHasMissingLayoutId_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var sellProfileService = new SellProfileTemplateService();
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

        var loader = new TemplateValidationLoader(itemService, mobileService, sellProfileService, bookTemplateService);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingItem_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var sellProfileService = new SellProfileTemplateService();
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

        var loader = new TemplateValidationLoader(itemService, mobileService, sellProfileService, bookTemplateService);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public void LoadAsync_WhenMobileReferencesMissingSellProfile_ShouldThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var sellProfileService = new SellProfileTemplateService();
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

        var loader = new TemplateValidationLoader(itemService, mobileService, sellProfileService, bookTemplateService);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await loader.LoadAsync());
    }

    [Test]
    public async Task LoadAsync_WhenTemplatesAreValid_ShouldNotThrow()
    {
        var itemService = new ItemTemplateService();
        var mobileService = new MobileTemplateService();
        var sellProfileService = new SellProfileTemplateService();
        using var tempDirectory = new TempDirectory();
        var bookTemplateService = CreateBookTemplateService(tempDirectory.Path);
        ContainerLayoutSystem.ContainerSizesById["backpack"] = new("backpack", 7, 4, "Backpack");

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
                ContainerLayoutId = "backpack"
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

        var loader = new TemplateValidationLoader(itemService, mobileService, sellProfileService, bookTemplateService);

        Assert.That(async () => await loader.LoadAsync(), Throws.Nothing);
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
