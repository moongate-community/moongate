using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public sealed class LootTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenTemplateDefinesRollsAndItemTag_ShouldLoadThoseFields()
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

        var lootDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "loot");
        Directory.CreateDirectory(lootDirectory);

        var filePath = Path.Combine(lootDirectory, "undead.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "loot",
                "id": "undead.low",
                "name": "Undead Low",
                "category": "loot",
                "description": "Undead corpse loot",
                "rolls": 2,
                "noDropWeight": 5,
                "entries": [
                  {
                    "weight": 10,
                    "itemTag": "weapon.rusty",
                    "amount": 1
                  }
                ]
              }
            ]
            """
        );

        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.That(lootTemplateService.TryGet("undead.low", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Rolls, Is.EqualTo(2));
                Assert.That(template.NoDropWeight, Is.EqualTo(5));
                Assert.That(template.Entries, Has.Count.EqualTo(1));
                Assert.That(template.Entries[0].ItemTag, Is.EqualTo("weapon.rusty"));
                Assert.That(template.Entries[0].ItemTemplateId, Is.Null);
                Assert.That(template.Entries[0].ItemId, Is.Null);
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenTemplateDefinesAdditiveMode_ShouldLoadChanceAndAmountRange()
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

        var lootDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "loot");
        Directory.CreateDirectory(lootDirectory);

        var filePath = Path.Combine(lootDirectory, "creatures.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "loot",
                "id": "undead.zombie",
                "name": "Zombie Loot",
                "category": "loot",
                "description": "",
                "mode": "additive",
                "entries": [
                  {
                    "itemTemplateId": "gold",
                    "chance": 0.5,
                    "amountMin": 20,
                    "amountMax": 50
                  }
                ]
              }
            ]
            """
        );

        var lootTemplateService = new LootTemplateService();
        var loader = new LootTemplateLoader(directoriesConfig, lootTemplateService);

        await loader.LoadAsync();

        Assert.That(lootTemplateService.TryGet("undead.zombie", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Mode, Is.EqualTo(LootTemplateMode.Additive));
                Assert.That(template.Rolls, Is.EqualTo(1));
                Assert.That(template.NoDropWeight, Is.EqualTo(0));
                Assert.That(template.Entries, Has.Count.EqualTo(1));
                Assert.That(template.Entries[0].ItemTemplateId, Is.EqualTo("gold"));
                Assert.That(template.Entries[0].Chance, Is.EqualTo(0.5));
                Assert.That(template.Entries[0].AmountMin, Is.EqualTo(20));
                Assert.That(template.Entries[0].AmountMax, Is.EqualTo(50));
            }
        );
    }
}
