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
    public async Task LoadAsync_WhenRepositoryContainsGeneratedMobiles_ShouldLoadRepresentativeVariantData()
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
                Assert.That(mobileTemplateService.TryGet("gypsy_npc", out var gypsy), Is.True);
                Assert.That(gypsy, Is.Not.Null);
                Assert.That(gypsy!.Variants, Is.Not.Empty);
                Assert.That(gypsy.Variants.Any(static variant => variant.Appearance.Body > 0), Is.True);
                Assert.That(gypsy.Variants.SelectMany(static variant => variant.Equipment), Is.Not.Empty);

                Assert.That(mobileTemplateService.TryGet("tropical_bird_npc", out var tropicalBird), Is.True);
                Assert.That(tropicalBird, Is.Not.Null);
                Assert.That(tropicalBird!.Variants, Has.Count.GreaterThanOrEqualTo(1));
                Assert.That(tropicalBird.Variants[0].Appearance.Body, Is.GreaterThan(0));

                Assert.That(mobileTemplateService.TryGet("bird_npc", out var bird), Is.True);
                Assert.That(bird, Is.Not.Null);
                Assert.That(bird!.Description, Does.Contain("Converted from ModernUO Bird."));
                Assert.That(tropicalBird.Title, Is.EqualTo("a tropical bird"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenGuardTemplatesUseBaseMobile_ShouldInheritDefaultFactionIdAndLootTables()
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

        await File.WriteAllTextAsync(
            Path.Combine(mobilesDirectory, "guards.json"),
            """
            [
              {
                "type": "mobile",
                "id": "base_guard",
                "name": "Base Guard",
                "defaultFactionId": "true_britannians",
                "lootTables": ["guard.warrior"],
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0190",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ]
              },
              {
                "type": "mobile",
                "id": "warrior_guard_male_npc",
                "base_mobile": "base_guard",
                "name": "a warrior guard"
              },
              {
                "type": "mobile",
                "id": "warrior_guard_female_npc",
                "base_mobile": "base_guard",
                "name": "a warrior guard"
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
                Assert.That(mobileTemplateService.TryGet("warrior_guard_male_npc", out var warriorMale), Is.True);
                Assert.That(warriorMale, Is.Not.Null);
                Assert.That(warriorMale!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(warriorMale.LootTables, Is.EqualTo(new[] { "guard.warrior" }));
                Assert.That(warriorMale.Variants, Has.Count.EqualTo(1));
                Assert.That(warriorMale.Variants[0].Appearance.Body, Is.EqualTo(0x0190));

                Assert.That(mobileTemplateService.TryGet("warrior_guard_female_npc", out var warriorFemale), Is.True);
                Assert.That(warriorFemale, Is.Not.Null);
                Assert.That(warriorFemale!.DefaultFactionId, Is.EqualTo("true_britannians"));
                Assert.That(warriorFemale.LootTables, Is.EqualTo(new[] { "guard.warrior" }));
                Assert.That(warriorFemale.Variants, Has.Count.EqualTo(1));
                Assert.That(warriorFemale.Variants[0].Appearance.Body, Is.EqualTo(0x0190));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenVendorTemplatesUseVariants_ShouldLoadVariantEquipment()
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

        await File.WriteAllTextAsync(
            Path.Combine(mobilesDirectory, "vendors.json"),
            """
            [
              {
                "type": "mobile",
                "id": "blacksmith_vendor_npc",
                "name": "a blacksmith",
                "defaultFactionId": "true_britannians",
                "sellProfileId": "vendor.blacksmith",
                "lootTables": ["vendor.blacksmith"],
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0190",
                      "skinHue": 0,
                      "hairHue": 0
                    },
                    "equipment": [
                      {
                        "layer": "Shirt",
                        "itemTemplateId": "fancy_shirt"
                      },
                      {
                        "layer": "OneHanded",
                        "items": [
                          {
                            "itemTemplateId": "tongs",
                            "weight": 1
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("blacksmith_vendor_npc", out var blacksmith), Is.True);
        Assert.That(blacksmith, Is.Not.Null);
        Assert.That(blacksmith!.DefaultFactionId, Is.EqualTo("true_britannians"));
        Assert.That(blacksmith.SellProfileId, Is.EqualTo("vendor.blacksmith"));
        Assert.That(blacksmith.LootTables, Is.EqualTo(new[] { "vendor.blacksmith" }));
        Assert.That(blacksmith.Title, Is.EqualTo("a blacksmith"));
        Assert.That(blacksmith.Variants, Has.Count.EqualTo(1));
        Assert.That(blacksmith.Variants[0].Equipment, Has.Count.EqualTo(2));
        Assert.That(blacksmith.Variants[0].Equipment[0].ItemTemplateId, Is.EqualTo("fancy_shirt"));
        Assert.That(blacksmith.Variants[0].Equipment[1].Items[0].ItemTemplateId, Is.EqualTo("tongs"));
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
                "defaultFactionId": "true_britannians",
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0190",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ]
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
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0011",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ],
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
                "sellProfileId": "vendor.blacksmith",
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0190",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ]
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

    private static string ResolveRepositoryRoot()
    {
        return Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..")
        );
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
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0003",
                      "skinHue": 0,
                      "hairHue": 0,
                      "hairStyle": 0
                    }
                  }
                ],
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
                "ai": {
                  "brain": "undead_melee",
                  "fightMode": "strongest",
                  "rangePerception": 14,
                  "rangeFight": 4
                },
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
                Assert.That(template.Variants, Has.Count.EqualTo(1));
                Assert.That(template.Variants[0].Appearance.Body, Is.EqualTo(0x0003));
                Assert.That(template.MinDamage, Is.EqualTo(5));
                Assert.That(template.MaxDamage, Is.EqualTo(10));
                Assert.That(template.ArmorRating, Is.EqualTo(20));
                Assert.That(template.Notoriety, Is.EqualTo(Notoriety.Murdered));
                Assert.That(template.Ai.Brain, Is.EqualTo("undead_melee"));
                Assert.That(template.Ai.FightMode, Is.EqualTo("strongest"));
                Assert.That(template.Ai.RangePerception, Is.EqualTo(14));
                Assert.That(template.Ai.RangeFight, Is.EqualTo(4));
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
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x11",
                      "skinHue": "hue(779:790)",
                      "hairHue": 0,
                      "hairStyle": 0
                    }
                  }
                ],
                "ai": {
                  "brain": "aggressive_orc"
                }
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
                Assert.That(definition?.Variants, Has.Count.EqualTo(1));
                Assert.That(definition?.Variants[0].Appearance.Body, Is.EqualTo(0x11));
                Assert.That(definition?.Variants[0].Appearance.SkinHue!.Value.IsRange, Is.True);
                Assert.That(definition?.Ai.Brain, Is.EqualTo("aggressive_orc"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileHasCanonicalAiDefaults_ShouldInheritAndPreserveExplicitOverrides()
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

        await File.WriteAllTextAsync(
            Path.Combine(mobilesDirectory, "ai.json"),
            """
            [
              {
                "type": "mobile",
                "id": "base_ai_guard",
                "name": "Base AI Guard",
                "ai": {
                  "brain": "ai_guard",
                  "fightMode": "strongest",
                  "rangePerception": 18,
                  "rangeFight": 4
                },
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0190",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ]
              },
              {
                "type": "mobile",
                "id": "guard_cadet",
                "base_mobile": "base_ai_guard",
                "name": "Guard Cadet",
                "ai": {
                  "brain": "none",
                  "fightMode": "closest",
                  "rangePerception": 16,
                  "rangeFight": 1
                }
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("guard_cadet", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Ai.Brain, Is.EqualTo("none"));
                Assert.That(template.Ai.FightMode, Is.EqualTo("closest"));
                Assert.That(template.Ai.RangePerception, Is.EqualTo(16));
                Assert.That(template.Ai.RangeFight, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseMobileHasResistancesAndDamageTypes_ShouldInheritAndOverride()
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

        var filePath = Path.Combine(mobilesDirectory, "resistances.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "mobile",
                "id": "base_elemental",
                "name": "Base Elemental",
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x000E",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ],
                "resistances": {
                  "Physical": 50,
                  "Fire": 30,
                  "Cold": 20,
                  "Poison": 10,
                  "Energy": 15
                },
                "damageTypes": {
                  "Physical": 100
                }
              },
              {
                "type": "mobile",
                "id": "fire_elemental",
                "base_mobile": "base_elemental",
                "name": "a fire elemental",
                "resistances": {
                  "Fire": 70
                },
                "damageTypes": {
                  "Physical": 25,
                  "Fire": 75
                }
              },
              {
                "type": "mobile",
                "id": "lesser_fire_elemental",
                "base_mobile": "base_elemental",
                "name": "a lesser fire elemental"
              }
            ]
            """
        );

        var mobileTemplateService = new MobileTemplateService();
        var loader = new MobileTemplateLoader(directoriesConfig, mobileTemplateService);

        await loader.LoadAsync();

        Assert.That(mobileTemplateService.TryGet("fire_elemental", out var fireElemental), Is.True);
        Assert.That(fireElemental, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                // Child overrides Fire resistance but inherits others from parent via TryAdd
                Assert.That(fireElemental!.Resistances["Fire"], Is.EqualTo(70));
                Assert.That(fireElemental.Resistances["Physical"], Is.EqualTo(50));
                Assert.That(fireElemental.Resistances["Cold"], Is.EqualTo(20));
                Assert.That(fireElemental.Resistances["Poison"], Is.EqualTo(10));
                Assert.That(fireElemental.Resistances["Energy"], Is.EqualTo(15));
                Assert.That(fireElemental.Resistances, Has.Count.EqualTo(5));

                // Child fully overrides damageTypes
                Assert.That(fireElemental.DamageTypes["Physical"], Is.EqualTo(25));
                Assert.That(fireElemental.DamageTypes["Fire"], Is.EqualTo(75));
                Assert.That(fireElemental.DamageTypes, Has.Count.EqualTo(2));
            }
        );

        // Lesser fire elemental inherits everything from parent
        Assert.That(mobileTemplateService.TryGet("lesser_fire_elemental", out var lesserFire), Is.True);
        Assert.That(lesserFire, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(lesserFire!.Resistances["Physical"], Is.EqualTo(50));
                Assert.That(lesserFire.Resistances["Fire"], Is.EqualTo(30));
                Assert.That(lesserFire.Resistances, Has.Count.EqualTo(5));
                Assert.That(lesserFire.DamageTypes["Physical"], Is.EqualTo(100));
                Assert.That(lesserFire.DamageTypes, Has.Count.EqualTo(1));
            }
        );
    }

}
