using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public class MobileTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenRepositoryContainsGuardTemplates_ShouldLoadDefaultFactionIdAndLootTables()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobileTemplateService.TryGet("warrior_guard_male_npc", out var warriorMale), Is.True);
                Assert.That(warriorMale, Is.Not.Null);
                Assert.That(warriorMale!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(warriorMale.LootTables, Is.EqualTo(new[] { "guard.warrior" }));

                Assert.That(mobileTemplateService.TryGet("warrior_guard_female_npc", out var warriorFemale), Is.True);
                Assert.That(warriorFemale, Is.Not.Null);
                Assert.That(warriorFemale!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(warriorFemale.LootTables, Is.EqualTo(new[] { "guard.warrior" }));

                Assert.That(mobileTemplateService.TryGet("archer_guard_male_npc", out var archerMale), Is.True);
                Assert.That(archerMale, Is.Not.Null);
                Assert.That(archerMale!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(archerMale.LootTables, Is.EqualTo(new[] { "guard.archer" }));

                Assert.That(mobileTemplateService.TryGet("archer_guard_female_npc", out var archerFemale), Is.True);
                Assert.That(archerFemale, Is.Not.Null);
                Assert.That(archerFemale!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(archerFemale.LootTables, Is.EqualTo(new[] { "guard.archer" }));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRepositoryContainsVendorTemplates_ShouldLoadVendorDefinitions()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var dataRoot = Path.Combine(repositoryRoot, "moongate_data");
        var directoriesConfig = new DirectoriesConfig(dataRoot, DirectoryType.Templates);
        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobileTemplateService.TryGet("blacksmith_vendor_npc", out var blacksmith), Is.True);
                Assert.That(blacksmith, Is.Not.Null);
                Assert.That(blacksmith!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(blacksmith.SellProfileId, Is.EqualTo("vendor.blacksmith"));
                Assert.That(blacksmith.LootTables, Is.EqualTo(new[] { "vendor.blacksmith" }));
                Assert.That(blacksmith.Title, Is.EqualTo("a blacksmith"));
                Assert.That(blacksmith.FixedEquipment, Has.Count.EqualTo(5));
                Assert.That(blacksmith.FixedEquipment[0].ItemTemplateId, Is.EqualTo("fancy_shirt"));
                Assert.That(blacksmith.FixedEquipment[4].ItemTemplateId, Is.EqualTo("tongs"));

                Assert.That(mobileTemplateService.TryGet("weaponsmith_vendor_npc", out var weaponsmith), Is.True);
                Assert.That(weaponsmith, Is.Not.Null);
                Assert.That(weaponsmith!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(weaponsmith.SellProfileId, Is.EqualTo("vendor.weaponsmith"));
                Assert.That(weaponsmith.LootTables, Is.EqualTo(new[] { "vendor.weaponsmith" }));
                Assert.That(weaponsmith.Strength, Is.EqualTo(80));
                Assert.That(weaponsmith.Dexterity, Is.EqualTo(60));
                Assert.That(weaponsmith.Intelligence, Is.EqualTo(45));
                Assert.That(weaponsmith.Hits, Is.EqualTo(80));
                Assert.That(weaponsmith.Title, Is.EqualTo("the weaponsmith"));
                Assert.That(weaponsmith.FixedEquipment, Has.Count.EqualTo(5));
                Assert.That(weaponsmith.FixedEquipment[4].ItemTemplateId, Is.EqualTo("hatchet"));

                Assert.That(mobileTemplateService.TryGet("armorer_vendor_npc", out var armorer), Is.True);
                Assert.That(armorer, Is.Not.Null);
                Assert.That(armorer!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(armorer.SellProfileId, Is.EqualTo("vendor.armorer"));
                Assert.That(armorer.LootTables, Is.EqualTo(new[] { "vendor.armorer" }));
                Assert.That(armorer.Title, Is.EqualTo("an armorer"));
                Assert.That(armorer.FixedEquipment, Has.Count.EqualTo(5));
                Assert.That(armorer.FixedEquipment[4].ItemTemplateId, Is.EqualTo("helmet"));

                Assert.That(mobileTemplateService.TryGet("provisioner_vendor_npc", out var provisioner), Is.True);
                Assert.That(provisioner, Is.Not.Null);
                Assert.That(provisioner!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(provisioner.SellProfileId, Is.EqualTo("vendor.provisioner"));
                Assert.That(provisioner.LootTables, Is.EqualTo(new[] { "vendor.provisioner" }));
                Assert.That(provisioner.Title, Is.EqualTo("the provisioner"));
                Assert.That(provisioner.FixedEquipment, Has.Count.EqualTo(4));
                Assert.That(provisioner.FixedEquipment[3].ItemTemplateId, Is.EqualTo("backpack"));

                Assert.That(mobileTemplateService.TryGet("mage_vendor_npc", out var mage), Is.True);
                Assert.That(mage, Is.Not.Null);
                Assert.That(mage!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(mage.SellProfileId, Is.EqualTo("vendor.mage"));
                Assert.That(mage.LootTables, Is.EqualTo(new[] { "vendor.mage" }));
                Assert.That(mage.Title, Is.EqualTo("a mage"));
                Assert.That(mage.FixedEquipment, Has.Count.EqualTo(4));
                Assert.That(mage.FixedEquipment[2].ItemTemplateId, Is.EqualTo("wizards_hat"));
                Assert.That(mage.FixedEquipment[3].ItemTemplateId, Is.EqualTo("spellbook"));

                Assert.That(mobileTemplateService.TryGet("healer_vendor_npc", out var healer), Is.True);
                Assert.That(healer, Is.Not.Null);
                Assert.That(healer!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(healer.SellProfileId, Is.EqualTo("vendor.healer"));
                Assert.That(healer.LootTables, Is.EqualTo(new[] { "vendor.healer" }));
                Assert.That(healer.Title, Is.EqualTo("a healer"));
                Assert.That(healer.FixedEquipment, Has.Count.EqualTo(2));
                Assert.That(healer.FixedEquipment[0].ItemTemplateId, Is.EqualTo("robe"));
                Assert.That(healer.FixedEquipment[1].ItemTemplateId, Is.EqualTo("sandals"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileHasDefaultFactionId_ShouldInheritDefaultFactionId()
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

        var filePath = Path.Combine(mobilesDirectory, "factions.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "base_faction_guard",
                "name": "Base Guard",
                "body": "0x0190",
                "defaultFactionId": "true_britannians"
              },
              {
                "type": "mobile",
                "id": "faction_guard",
                "base_mobile": "base_faction_guard",
                "name": "Faction Guard"
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("faction_guard", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.DefaultFactionId, Is.EqualTo("true_britannians"));
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileHasParams_ShouldInheritAndOverrideParams()
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

        var filePath = Path.Combine(mobilesDirectory, "params.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "base_orc",
                "name": "Base Orc",
                "body": "0x0011",
                "params": {
                  "title_suffix": { "type": "string", "value": "the grim" },
                  "owner_id": { "type": "serial", "value": "0x00001000" }
                }
              },
              {
                "type": "mobile",
                "id": "orc_warrior",
                "base_mobile": "base_orc",
                "name": "Orc Warrior",
                "params": {
                  "owner_id": { "type": "serial", "value": "0x00002000" },
                  "marker_hue": { "type": "hue", "value": "0x044D" }
                }
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("orc_warrior", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Params, Has.Count.EqualTo(3));
                Assert.That(template.Params.ContainsKey("title_suffix"), Is.True);
                Assert.That(template.Params["title_suffix"].Type, Is.EqualTo(ItemTemplateParamType.String));
                Assert.That(template.Params["title_suffix"].Value, Is.EqualTo("the grim"));
                Assert.That(template.Params["owner_id"].Type, Is.EqualTo(ItemTemplateParamType.Serial));
                Assert.That(template.Params["owner_id"].Value, Is.EqualTo("0x00002000"));
                Assert.That(template.Params["marker_hue"].Type, Is.EqualTo(ItemTemplateParamType.Hue));
                Assert.That(template.Params["marker_hue"].Value, Is.EqualTo("0x044D"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileHasSellProfile_ShouldInheritSellProfileId()
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

        var filePath = Path.Combine(mobilesDirectory, "vendors.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "base_vendor",
                "name": "Base Vendor",
                "body": "0x0190",
                "sellProfileId": "vendor.blacksmith"
              },
              {
                "type": "mobile",
                "id": "blacksmith_vendor",
                "base_mobile": "base_vendor",
                "name": "Blacksmith Vendor"
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("blacksmith_vendor", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.SellProfileId, Is.EqualTo("vendor.blacksmith"));
    }

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
