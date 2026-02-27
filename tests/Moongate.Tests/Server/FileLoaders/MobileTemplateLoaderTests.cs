using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public class MobileTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenBaseMobileIsDefined_ShouldInheritParentValues()
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

        var mobilesDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "mobiles");
        Directory.CreateDirectory(mobilesDirectory);

        var filePath = Path.Combine(mobilesDirectory, "undead.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "base_undead",
                "name": "Base Undead",
                "category": "undead",
                "description": "base",
                "tags": ["undead"],
                "body": "0x0003",
                "skinHue": 0,
                "hairHue": 0,
                "hairStyle": 0,
                "strength": 100,
                "dexterity": 80,
                "intelligence": 60,
                "hits": 120,
                "minDamage": 5,
                "maxDamage": 10,
                "armorRating": 20,
                "fame": 600,
                "karma": -600,
                "notoriety": "Murdered",
                "brain": "undead_melee",
                "sounds": {
                  "StartAttack": 471,
                  "Idle": 472,
                  "Attack": 473,
                  "Defend": 474,
                  "Die": 475
                },
                "goldDrop": "dice(1d13+3)",
                "lootTables": ["bonearmor"],
                "skills": { "wrestling": 500 }
              },
              {
                "type": "mobile",
                "id": "zombie",
                "base_mobile": "base_undead",
                "name": "a zombie",
                "strength": 110,
                "sounds": { "Attack": 601 },
                "skills": { "tactics": 450 }
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("zombie", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Strength, Is.EqualTo(110));
                Assert.That(template.Category, Is.EqualTo("undead"));
                Assert.That(template.Body, Is.EqualTo(0x0003));
                Assert.That(template.MinDamage, Is.EqualTo(5));
                Assert.That(template.MaxDamage, Is.EqualTo(10));
                Assert.That(template.ArmorRating, Is.EqualTo(20));
                Assert.That(template.Notoriety, Is.EqualTo(Notoriety.Murdered));
                Assert.That(template.Brain, Is.EqualTo("undead_melee"));
                Assert.That(template.LootTables, Is.EquivalentTo(new[] { "bonearmor" }));
                Assert.That(template.Sounds[MobileSoundType.StartAttack], Is.EqualTo(471));
                Assert.That(template.Sounds[MobileSoundType.Attack], Is.EqualTo(601));
                Assert.That(template.Skills["wrestling"], Is.EqualTo(500));
                Assert.That(template.Skills["tactics"], Is.EqualTo(450));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileIsMissing_ShouldThrow()
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

        var mobilesDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "mobiles");
        Directory.CreateDirectory(mobilesDirectory);

        var filePath = Path.Combine(mobilesDirectory, "invalid.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "zombie",
                "base_mobile": "does_not_exist",
                "name": "a zombie"
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        Assert.That(
            async () => await loader.LoadAsync(),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("unknown base_mobile")
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileReferencesAreCircular_ShouldThrow()
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

        var mobilesDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "mobiles");
        Directory.CreateDirectory(mobilesDirectory);

        var filePath = Path.Combine(mobilesDirectory, "cycle.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "a",
                "base_mobile": "b"
              },
              {
                "type": "mobile",
                "id": "b",
                "base_mobile": "a"
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        Assert.That(
            async () => await loader.LoadAsync(),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Circular base_mobile")
        );
    }

    [Test]
    public void LoadAsync_WhenDirectoryMissing_ShouldNotThrow()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, DirectoryType.Templates);
        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        Assert.That(async () => await loader.LoadAsync(), Throws.Nothing);
        Assert.That(mobileTemplateService.Count, Is.Zero);
    }

    [Test]
    public async Task LoadAsync_WhenTemplateFilesExist_ShouldPopulateTemplateService()
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

        var mobilesDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "mobiles");
        Directory.CreateDirectory(mobilesDirectory);

        var filePath = Path.Combine(mobilesDirectory, "orcs.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "orc_warrior",
                "name": "Orc Warrior",
                "category": "monsters",
                "description": "Orc melee unit",
                "tags": ["orc"],
                "body": "0x11",
                "skinHue": "hue(779:790)",
                "hairHue": 0,
                "hairStyle": 0,
                "brain": "aggressive_orc"
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobileTemplateService.Count, Is.EqualTo(1));
                Assert.That(mobileTemplateService.TryGet("orc_warrior", out var definition), Is.True);
                Assert.That(definition?.Body, Is.EqualTo(0x11));
                Assert.That(definition?.SkinHue.IsRange, Is.True);
                Assert.That(definition?.Brain, Is.EqualTo("aggressive_orc"));
            }
        );
    }
}
