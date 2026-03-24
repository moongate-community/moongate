using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.FileLoaders;

public class ItemTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenBaseItemHasBookId_ShouldInheritFromParent()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "books");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "books.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "books",
                "id": "base_book",
                "name": "Base Book",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0FF0",
                "lootType": "Regular",
                "scriptId": "none",
                "bookId": "welcome_player",
                "tags": ["book"],
                "weight": 1.0
              },
              {
                "type": "item",
                "category": "books",
                "id": "derived_book",
                "base_item": "base_book",
                "name": "Derived Book",
                "description": "derived",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0FF1",
                "lootType": "Regular",
                "scriptId": "none",
                "tags": ["book"],
                "weight": 1.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("derived_book", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.BookId, Is.EqualTo("welcome_player"));
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemHasFlippableItemIds_ShouldInheritFromParent()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "base");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "flippable.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "test",
                "id": "base_door",
                "name": "Base Door",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0675",
                "lootType": "Regular",
                "scriptId": "items.door",
                "tags": [],
                "weight": 1.0,
                "flippableItemIds": ["0x0675", "0x0676"]
              },
              {
                "type": "item",
                "category": "test",
                "id": "door_child",
                "base_item": "base_door",
                "name": "Door Child",
                "description": "child",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0677",
                "lootType": "Regular",
                "scriptId": "items.door_child",
                "tags": [],
                "weight": 1.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("door_child", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.FlippableItemIds, Is.EqualTo(new[] { "0x0675", "0x0676" }));
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemHasParams_ShouldInheritAndOverrideParams()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "base");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "params.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "test",
                "id": "base_item",
                "name": "Base Item",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x1517",
                "lootType": "Regular",
                "scriptId": "items.base_item",
                "tags": [],
                "weight": 1.0,
                "params": {
                  "label_number": { "type": "string", "value": "#1000001" },
                  "linked_id": { "type": "serial", "value": "0x40000001" }
                }
              },
              {
                "type": "item",
                "category": "test",
                "id": "child_item",
                "base_item": "base_item",
                "name": "Child Item",
                "description": "child",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x1518",
                "lootType": "Regular",
                "scriptId": "items.child_item",
                "tags": [],
                "weight": 1.0,
                "params": {
                  "linked_id": { "type": "serial", "value": "0x40000002" },
                  "tint": { "type": "hue", "value": "0x044D" }
                }
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("child_item", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Params, Has.Count.EqualTo(3));
                Assert.That(template.Params.ContainsKey("label_number"), Is.True);
                Assert.That(template.Params["label_number"].Type, Is.EqualTo(ItemTemplateParamType.String));
                Assert.That(template.Params["label_number"].Value, Is.EqualTo("#1000001"));
                Assert.That(template.Params["linked_id"].Type, Is.EqualTo(ItemTemplateParamType.Serial));
                Assert.That(template.Params["linked_id"].Value, Is.EqualTo("0x40000002"));
                Assert.That(template.Params["tint"].Type, Is.EqualTo(ItemTemplateParamType.Hue));
                Assert.That(template.Params["tint"].Value, Is.EqualTo("0x044D"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemHasQuiverStats_ShouldInheritFromParent()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "base");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "quiver.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "test",
                "id": "base_quiver",
                "name": "Base Quiver",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x2FB7",
                "gumpId": "0x0108",
                "lootType": "Regular",
                "scriptId": "items.base_quiver",
                "tags": ["quiver"],
                "isQuiver": true,
                "lowerAmmoCost": 20,
                "quiverDamageIncrease": 10,
                "weightReduction": 30,
                "weight": 2.0
              },
              {
                "type": "item",
                "category": "test",
                "id": "derived_quiver",
                "base_item": "base_quiver",
                "name": "Derived Quiver",
                "description": "derived",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x2B02",
                "lootType": "Regular",
                "scriptId": "items.derived_quiver",
                "tags": ["quiver"],
                "weight": 8.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("derived_quiver", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.IsQuiver, Is.True);
                Assert.That(template.LowerAmmoCost, Is.EqualTo(20));
                Assert.That(template.QuiverDamageIncrease, Is.EqualTo(10));
                Assert.That(template.WeightReduction, Is.EqualTo(30));
                Assert.That(template.GumpId, Is.EqualTo("0x0108"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemHasWeaponSkill_ShouldInheritFromParent()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "base");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "weapon_skill.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "test",
                "id": "base_bow",
                "name": "Base Bow",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x13B2",
                "lootType": "Regular",
                "scriptId": "items.base_bow",
                "tags": ["weapon"],
                "weaponSkill": "Archery",
                "ammo": "0x0F3F",
                "ammoFx": "0x1BFE",
                "weight": 6.0
              },
              {
                "type": "item",
                "category": "test",
                "id": "derived_bow",
                "base_item": "base_bow",
                "name": "Derived Bow",
                "description": "derived",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x13B3",
                "lootType": "Regular",
                "scriptId": "items.derived_bow",
                "tags": ["weapon"],
                "weight": 6.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("derived_bow", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.WeaponSkill, Is.EqualTo(UOSkillName.Archery));
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemHasWeaponSounds_ShouldInheritFromParent()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "weapons");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "weapon_sounds.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "weapons",
                "id": "base_bow",
                "name": "Base Bow",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x13B2",
                "lootType": "Regular",
                "scriptId": "none",
                "tags": ["weapon"],
                "hitSound": 564,
                "missSound": 568,
                "weight": 6.0
              },
              {
                "type": "item",
                "category": "weapons",
                "id": "derived_bow",
                "base_item": "base_bow",
                "name": "Derived Bow",
                "description": "derived",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x13B3",
                "lootType": "Regular",
                "scriptId": "none",
                "tags": ["weapon"],
                "weight": 6.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("derived_bow", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.HitSound, Is.EqualTo(564));
                Assert.That(template.MissSound, Is.EqualTo(568));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemIsDefined_ShouldInheritParentValues()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "weapons");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "weapons.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "weapons",
                "id": "base_cutlass",
                "name": "cutlass",
                "description": "base weapon",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x1440",
                "lootType": "Regular",
                "scriptId": "base_script",
                "stackable": false,
                "tags": ["weapon"],
                "weight": 500,
                "weightMax": 40000,
                "maxItems": 125,
                "lowDamage": 4,
                "highDamage": 8,
                "defense": 12,
                "hitPoints": 30,
                "speed": 35,
                "strength": 20,
                "strengthAdd": 2,
                "dexterity": 10,
                "dexterityAdd": 1,
                "intelligence": 0,
                "intelligenceAdd": 0,
                "physicalResist": 10,
                "fireResist": 8,
                "coldResist": 6,
                "poisonResist": 4,
                "energyResist": 2,
                "hitChanceIncrease": 12,
                "defenseChanceIncrease": 7,
                "damageIncrease": 15,
                "swingSpeedIncrease": 20,
                "spellDamageIncrease": 25,
                "fasterCasting": 2,
                "fasterCastRecovery": 3,
                "lowerManaCost": 5,
                "lowerReagentCost": 10,
                "luck": 100,
                "spellChanneling": true,
                "usesRemaining": 30,
                "ammo": 0,
                "ammoFx": 0,
                "maxRange": 1,
                "baseRange": 1
              },
              {
                "type": "item",
                "category": "weapons",
                "id": "cutlass_variant",
                "base_item": "base_cutlass",
                "name": "cutlass variant",
                "description": "variant",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x1441",
                "lootType": "Regular",
                "scriptId": "",
                "stackable": false,
                "tags": [],
                "weight": 600,
                "lowDamage": 5
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("cutlass_variant", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.ItemId, Is.EqualTo("0x1441"));
                Assert.That(template.ScriptId, Is.EqualTo("base_script"));
                Assert.That(template.Weight, Is.EqualTo(600));
                Assert.That(template.LowDamage, Is.EqualTo(5));
                Assert.That(template.HighDamage, Is.EqualTo(8));
                Assert.That(template.Defense, Is.EqualTo(12));
                Assert.That(template.WeightMax, Is.EqualTo(40000));
                Assert.That(template.MaxItems, Is.EqualTo(125));
                Assert.That(template.Strength, Is.EqualTo(20));
                Assert.That(template.StrengthAdd, Is.EqualTo(2));
                Assert.That(template.PhysicalResist, Is.EqualTo(10));
                Assert.That(template.FireResist, Is.EqualTo(8));
                Assert.That(template.ColdResist, Is.EqualTo(6));
                Assert.That(template.PoisonResist, Is.EqualTo(4));
                Assert.That(template.EnergyResist, Is.EqualTo(2));
                Assert.That(template.HitChanceIncrease, Is.EqualTo(12));
                Assert.That(template.DefenseChanceIncrease, Is.EqualTo(7));
                Assert.That(template.DamageIncrease, Is.EqualTo(15));
                Assert.That(template.SwingSpeedIncrease, Is.EqualTo(20));
                Assert.That(template.SpellDamageIncrease, Is.EqualTo(25));
                Assert.That(template.FasterCasting, Is.EqualTo(2));
                Assert.That(template.FasterCastRecovery, Is.EqualTo(3));
                Assert.That(template.LowerManaCost, Is.EqualTo(5));
                Assert.That(template.LowerReagentCost, Is.EqualTo(10));
                Assert.That(template.Luck, Is.EqualTo(100));
                Assert.That(template.SpellChanneling, Is.True);
                Assert.That(template.UsesRemaining, Is.EqualTo(30));
                Assert.That(template.MaxRange, Is.EqualTo(1));
                Assert.That(template.BaseRange, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemIsMissing_ShouldThrow()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "invalid.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "weapons",
                "id": "cutlass_variant",
                "name": "cutlass variant",
                "base_item": "missing_base"
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        Assert.That(
            async () => await loader.LoadAsync(),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("unknown base_item")
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseItemReferencesAreCircular_ShouldThrow()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "cycle.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "weapons",
                "id": "item_a",
                "name": "A",
                "base_item": "item_b"
              },
              {
                "type": "item",
                "category": "weapons",
                "id": "item_b",
                "name": "B",
                "base_item": "item_a"
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        Assert.That(
            async () => await loader.LoadAsync(),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Circular base_item")
        );
    }

    [Test]
    public async Task LoadAsync_WhenBaseTestContainersAreLoaded_ShouldResolveLootTestChestWithoutMissingParent()
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

        var rootItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        var baseItemsDirectory = Path.Combine(rootItemsDirectory, "base");
        Directory.CreateDirectory(rootItemsDirectory);
        Directory.CreateDirectory(baseItemsDirectory);

        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "base",
                    "test_containers.json"
                )
            ),
            Path.Combine(baseItemsDirectory, "test_containers.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("loot_test_chest", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(template!.ItemId, Is.EqualTo("0x0E80"));
                Assert.That(template.ContainerLayoutId, Is.EqualTo("metal_chest"));
                Assert.That(template.LootTables, Is.EqualTo(new[] { "loot_test_chest_basic" }));
                Assert.That(template.ScriptId, Is.EqualTo("none"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenDerivedBulletinBoardUsesBaseItem_ShouldInheritBoardValues()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "test");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "bulletin_boards.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "id": "bulletin_board",
                "name": "Bulletin Board",
                "category": "Bulletin Boards",
                "description": "base",
                "itemId": "0x1E5E",
                "hue": "0",
                "goldValue": "0",
                "weight": 0,
                "scriptId": "items.bulletin_board",
                "isMovable": true,
                "tags": ["modernuo", "bulletin boards", "flippable"]
              },
              {
                "type": "item",
                "id": "bb",
                "base_item": "bulletin_board",
                "name": "BB",
                "category": "Bulletin Boards",
                "description": "derived",
                "itemId": "0x1E5E",
                "scriptId": "items.bb",
                "tags": ["test"]
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("bb", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.ItemId, Is.EqualTo("0x1E5E"));
                Assert.That(template.Weight, Is.EqualTo(0m));
                Assert.That(template.ScriptId, Is.EqualTo("items.bb"));
                Assert.That(template.Tags, Does.Contain("test"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenDerivedItemUsesBaseItem_ShouldInheritSkillItemValues()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "test");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "dye.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "skill items",
                "id": "dye_tub",
                "name": "Dye Tub",
                "description": "base",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0FAB",
                "lootType": "Regular",
                "scriptId": "items.dye_tub",
                "tags": ["modernuo", "skill items"],
                "weight": 10.0
              },
              {
                "type": "item",
                "category": "skill items",
                "id": "dye_box",
                "base_item": "dye_tub",
                "name": "Dye Box",
                "description": "derived",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0FAB",
                "lootType": "Regular",
                "scriptId": "items.dye_box",
                "tags": ["test"],
                "weight": 10.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("dye_box", out var template), Is.True);
        Assert.That(template, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(template!.ItemId, Is.EqualTo("0x0FAB"));
                Assert.That(template.Weight, Is.EqualTo(10.0m));
                Assert.That(template.ScriptId, Is.EqualTo("items.dye_box"));
                Assert.That(template.Tags, Does.Contain("test"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenItemDefinesLootTables_ShouldLoadThem()
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "containers");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "loot_chest.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "containers",
                "id": "loot_chest",
                "name": "Loot Chest",
                "description": "random chest",
                "container": [],
                "lootTables": ["minor_treasure", "bandit_supplies"],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x0E40",
                "lootType": "Regular",
                "scriptId": "items.loot_chest",
                "tags": ["container"],
                "weight": 1.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("loot_chest", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.That(template!.LootTables, Is.EqualTo(new[] { "minor_treasure", "bandit_supplies" }));
    }

    [Test]
    public void LoadAsync_WhenItemsDirectoryDoesNotExist_ShouldNotThrow()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, DirectoryType.Templates);
        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        Assert.That(async () => await loader.LoadAsync(), Throws.Nothing);
        Assert.That(itemTemplateService.Count, Is.Zero);
    }

    [Test]
    public async Task LoadAsync_WhenRootContainersTemplateContainsBackpack_ShouldExposeContainerMetadata()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "containers.json"
                )
            ),
            Path.Combine(targetItemsDirectory, "containers.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("backpack", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(template!.MaxItems, Is.GreaterThan(0));
                Assert.That(template.WeightMax, Is.GreaterThan(0));
                Assert.That(template.ScriptId, Is.EqualTo("none"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootItemTemplatesContainFillableDependencies_ShouldExposeMissingVendorItems()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);

        foreach (var fileName in new[] { "skill_items.json", "special.json" })
        {
            File.Copy(
                Path.GetFullPath(
                    Path.Combine(
                        TestContext.CurrentContext.TestDirectory,
                        "..",
                        "..",
                        "..",
                        "..",
                        "..",
                        "moongate_data",
                        "templates",
                        "items",
                        fileName
                    )
                ),
                Path.Combine(targetItemsDirectory, fileName),
                true
            );
        }

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(itemTemplateService.TryGet("iron_ingot", out _), Is.True);
                Assert.That(itemTemplateService.TryGet("leather", out _), Is.True);
                Assert.That(itemTemplateService.TryGet("spellbook", out _), Is.True);
                Assert.That(itemTemplateService.TryGet("clock", out var clock), Is.True);
                Assert.That(clock, Is.Not.Null);
                Assert.That(clock!.ScriptId, Is.EqualTo("items.clock"));
                Assert.That(itemTemplateService.TryGet("tool_kit", out _), Is.True);
                Assert.That(itemTemplateService.TryGet("deco_arrow_shafts", out _), Is.True);
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootLightsTemplateIsToggleable_ShouldExposeSharedScriptAndLightParams()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "lights.json"
                )
            ),
            Path.Combine(targetItemsDirectory, "lights.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("candle", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(template!.ScriptId, Is.EqualTo("items.light_source"));
                Assert.That(template.Params.ContainsKey("light_lit_item_id"), Is.True);
                Assert.That(template.Params.ContainsKey("light_unlit_item_id"), Is.True);
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootMapsTemplateContainsTreasureMap_ShouldExposeDerivedTemplate()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "maps.json"
                )
            ),
            Path.Combine(targetItemsDirectory, "maps.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("treasure_map", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(template!.ItemId, Is.EqualTo("0x14EC"));
                Assert.That(template.ScriptId, Is.EqualTo("none"));
                Assert.That(template.Tags, Does.Contain("maps"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootMiscAndBaseTeleportsAreLoaded_ShouldResolveSingleTeleporterTemplate()
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

        var rootItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        var baseItemsDirectory = Path.Combine(rootItemsDirectory, "base");
        Directory.CreateDirectory(rootItemsDirectory);
        Directory.CreateDirectory(baseItemsDirectory);

        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "misc.json"
                )
            ),
            Path.Combine(rootItemsDirectory, "misc.json"),
            true
        );

        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "base",
                    "teleports.json"
                )
            ),
            Path.Combine(baseItemsDirectory, "teleports.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("teleporter", out var teleporter), Is.True);
        Assert.That(teleporter, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(teleporter!.ScriptId, Is.EqualTo("items.teleport"));
                Assert.That(teleporter.Category, Is.EqualTo("Structure"));
                Assert.That(itemTemplateService.TryGet("moongate", out _), Is.True);
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootShieldsTemplateContainsBuckler_ShouldExposeLayerAndStrength()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "shields.json"
                )
            ),
            Path.Combine(targetItemsDirectory, "shields.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("buckler", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Layer, Is.EqualTo(ItemLayerType.TwoHanded));
                Assert.That(template.Strength, Is.GreaterThan(0));
                Assert.That(template.ScriptId, Is.EqualTo("none"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootSkillItemsTemplateContainsBasePotions_ShouldExposePotionTemplates()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "skill_items.json"
                )
            ),
            Path.Combine(targetItemsDirectory, "skill_items.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(itemTemplateService.TryGet("lesser_heal_potion", out var healPotion), Is.True);
                Assert.That(healPotion, Is.Not.Null);
                Assert.That(healPotion!.ItemId, Is.EqualTo("0x0F0C"));
                Assert.That(healPotion.ScriptId, Is.EqualTo("items.lesser_heal_potion"));

                Assert.That(itemTemplateService.TryGet("refresh_potion", out var refreshPotion), Is.True);
                Assert.That(refreshPotion, Is.Not.Null);
                Assert.That(refreshPotion!.ScriptId, Is.EqualTo("items.refresh_potion"));

                Assert.That(itemTemplateService.TryGet("lesser_explosion_potion", out var explosionPotion), Is.True);
                Assert.That(explosionPotion, Is.Not.Null);
                Assert.That(explosionPotion!.Tags, Does.Contain("potion"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenRootWeaponsTemplateContainsBow_ShouldExposeWeaponLayerAndSkill()
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

        var targetItemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items");
        Directory.CreateDirectory(targetItemsDirectory);
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "templates",
                    "items",
                    "weapons.json"
                )
            ),
            Path.Combine(targetItemsDirectory, "weapons.json"),
            true
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.That(itemTemplateService.TryGet("bow", out var template), Is.True);
        Assert.That(template, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(template!.Layer, Is.EqualTo(ItemLayerType.TwoHanded));
                Assert.That(template.WeaponSkill, Is.EqualTo(UOSkillName.Archery));
                Assert.That(template.MaxRange, Is.GreaterThan(1));
            }
        );
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

        var itemsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "items", "clothes");
        Directory.CreateDirectory(itemsDirectory);

        var filePath = Path.Combine(itemsDirectory, "startup.json");
        await File.WriteAllTextAsync(
            filePath,
            """
            [
              {
                "type": "item",
                "category": "clothes",
                "id": "item.startup.shirt",
                "name": "Startup Shirt",
                "description": "Starter shirt",
                "container": [],
                "dyeable": true,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x1517",
                "lootType": "Regular",
                "scriptId": "none",
                "stackable": false,
                "tags": [],
                "weight": 1.0
              }
            ]
            """
        );

        var itemTemplateService = new ItemTemplateService();
        var loader = new ItemTemplateLoader(directoriesConfig, itemTemplateService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(itemTemplateService.Count, Is.EqualTo(1));
                Assert.That(itemTemplateService.TryGet("item.startup.shirt", out var template), Is.True);
                Assert.That(template?.ItemId, Is.EqualTo("0x1517"));
                Assert.That(template?.LootType, Is.EqualTo(LootType.Regular));
                Assert.That(template?.Hue.IsRange, Is.False);
                Assert.That(template?.Hue.Resolve(), Is.EqualTo(0));
                Assert.That(template?.GoldValue.IsDiceExpression, Is.False);
                Assert.That(template?.GoldValue.Resolve(), Is.EqualTo(0));
            }
        );
    }
}
