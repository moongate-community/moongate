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
}
