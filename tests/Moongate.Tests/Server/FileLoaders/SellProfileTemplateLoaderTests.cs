using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.Tests.Server.FileLoaders;

public class SellProfileTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_ShouldLoadSellProfilesFromTemplatesDirectory()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(
            tempDirectory.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        var profilesDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "sell_profiles");
        Directory.CreateDirectory(profilesDirectory);

        var filePath = Path.Combine(profilesDirectory, "blacksmith.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "sell_profile",
                "id": "vendor.blacksmith",
                "name": "Blacksmith Vendor",
                "category": "vendors",
                "description": "Blacksmith profile",
                "vendorItems": [
                  { "itemTemplateId": "longsword", "price": 55, "maxStock": 20 }
                ],
                "acceptedItems": [
                  { "itemTemplateId": "ingot_iron", "price": 3 }
                ]
              }
            ]
            """
        );

        var service = new SellProfileTemplateService();
        var loader = new SellProfileTemplateLoader(directoriesConfig, service);

        await loader.LoadAsync();

        Assert.That(service.TryGet("vendor.blacksmith", out var profile), Is.True);
        Assert.That(profile, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(profile!.VendorItems, Has.Count.EqualTo(1));
                Assert.That(profile.VendorItems[0].ItemTemplateId, Is.EqualTo("longsword"));
                Assert.That(profile.AcceptedItems, Has.Count.EqualTo(1));
                Assert.That(profile.AcceptedItems[0].ItemTemplateId, Is.EqualTo("ingot_iron"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRepositoryContainsVendorSellProfiles_ShouldLoadVendorDefinitions()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var service = new SellProfileTemplateService();
        var loader = new SellProfileTemplateLoader(directoriesConfig, service);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(service.TryGet("vendor.blacksmith", out var blacksmith), Is.True);
                Assert.That(blacksmith, Is.Not.Null);
                Assert.That(blacksmith!.VendorItems, Has.Count.EqualTo(5));
                Assert.That(blacksmith.VendorItems[0].ItemTemplateId, Is.EqualTo("hammer"));
                Assert.That(blacksmith.VendorItems[4].ItemTemplateId, Is.EqualTo("shovel"));
                Assert.That(blacksmith.AcceptedItems, Has.Count.EqualTo(2));
                Assert.That(blacksmith.AcceptedItems[0].ItemTemplateId, Is.EqualTo("iron_ingot"));

                Assert.That(service.TryGet("vendor.weaponsmith", out var weaponsmith), Is.True);
                Assert.That(weaponsmith, Is.Not.Null);
                Assert.That(weaponsmith!.VendorItems, Has.Count.EqualTo(5));
                Assert.That(weaponsmith.VendorItems[0].ItemTemplateId, Is.EqualTo("dagger"));
                Assert.That(weaponsmith.VendorItems[4].ItemTemplateId, Is.EqualTo("arrow"));

                Assert.That(service.TryGet("vendor.armorer", out var armorer), Is.True);
                Assert.That(armorer, Is.Not.Null);
                Assert.That(armorer!.VendorItems, Has.Count.EqualTo(5));
                Assert.That(armorer.VendorItems[0].ItemTemplateId, Is.EqualTo("helmet"));
                Assert.That(armorer.VendorItems[4].ItemTemplateId, Is.EqualTo("metal_shield"));
                Assert.That(armorer.AcceptedItems, Has.Count.EqualTo(4));

                Assert.That(service.TryGet("vendor.provisioner", out var provisioner), Is.True);
                Assert.That(provisioner, Is.Not.Null);
                Assert.That(provisioner!.VendorItems, Has.Count.EqualTo(5));
                Assert.That(provisioner.VendorItems[0].ItemTemplateId, Is.EqualTo("apple"));
                Assert.That(provisioner.VendorItems[4].ItemTemplateId, Is.EqualTo("candle"));
                Assert.That(provisioner.AcceptedItems, Has.Count.EqualTo(4));

                Assert.That(service.TryGet("vendor.mage", out var mage), Is.True);
                Assert.That(mage, Is.Not.Null);
                Assert.That(mage!.VendorItems, Has.Count.EqualTo(5));
                Assert.That(mage.VendorItems[0].ItemTemplateId, Is.EqualTo("spellbook"));
                Assert.That(mage.VendorItems[4].ItemTemplateId, Is.EqualTo("sulfurous_ash"));
                Assert.That(mage.AcceptedItems, Has.Count.EqualTo(4));

                Assert.That(service.TryGet("vendor.healer", out var healer), Is.True);
                Assert.That(healer, Is.Not.Null);
                Assert.That(healer!.VendorItems, Has.Count.EqualTo(4));
                Assert.That(healer.VendorItems[0].ItemTemplateId, Is.EqualTo("bandage"));
                Assert.That(healer.VendorItems[3].ItemTemplateId, Is.EqualTo("greater_heal_scroll"));
                Assert.That(healer.AcceptedItems, Has.Count.EqualTo(3));
            }
        );
    }

    [Test]
    public async Task LoadSingleAsync_WhenOtherFileWasRemoved_ShouldPreserveExistingProfiles()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(
            tempDirectory.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        var profilesDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "sell_profiles");
        Directory.CreateDirectory(profilesDirectory);

        var blacksmithPath = Path.Combine(profilesDirectory, "blacksmith.json");
        var healerPath = Path.Combine(profilesDirectory, "healer.json");

        await File.WriteAllTextAsync(
            blacksmithPath,
            """
            [
              {
                "type": "sell_profile",
                "id": "vendor.blacksmith",
                "name": "Blacksmith Vendor",
                "category": "vendors",
                "description": "Blacksmith profile",
                "vendorItems": [
                  { "itemTemplateId": "longsword", "price": 55, "maxStock": 20 }
                ]
              }
            ]
            """
        );
        await File.WriteAllTextAsync(
            healerPath,
            """
            [
              {
                "type": "sell_profile",
                "id": "vendor.healer",
                "name": "Healer Vendor",
                "category": "vendors",
                "description": "Healer profile",
                "vendorItems": [
                  { "itemTemplateId": "bandage", "price": 5, "maxStock": 50 }
                ]
              }
            ]
            """
        );

        var service = new SellProfileTemplateService();
        var loader = new SellProfileTemplateLoader(directoriesConfig, service);

        await loader.LoadAsync();
        File.Delete(healerPath);
        await File.WriteAllTextAsync(
            blacksmithPath,
            """
            [
              {
                "type": "sell_profile",
                "id": "vendor.blacksmith",
                "name": "Updated Blacksmith Vendor",
                "category": "vendors",
                "description": "Updated blacksmith profile",
                "vendorItems": [
                  { "itemTemplateId": "war_axe", "price": 80, "maxStock": 10 }
                ]
              }
            ]
            """
        );

        await loader.LoadSingleAsync(blacksmithPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(service.TryGet("vendor.blacksmith", out var blacksmith), Is.True);
                Assert.That(blacksmith, Is.Not.Null);
                Assert.That(blacksmith!.Name, Is.EqualTo("Updated Blacksmith Vendor"));
                Assert.That(blacksmith.VendorItems[0].ItemTemplateId, Is.EqualTo("war_axe"));
                Assert.That(service.TryGet("vendor.healer", out var healer), Is.True);
                Assert.That(healer, Is.Not.Null);
                Assert.That(service.Count, Is.EqualTo(2));
            }
        );
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Moongate.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test base directory.");
    }
}
