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
}
