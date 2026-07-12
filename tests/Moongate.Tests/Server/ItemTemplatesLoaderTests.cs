using Moongate.Server.Loaders;
using Moongate.Server.Services;
using Moongate.Ultima.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class ItemTemplatesLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_Seeds49FilesAndRegisters1664()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var service = new ItemTemplateService();
        var loader = new ItemTemplatesLoader(service, directories);
        var templatesDirectory = Path.Combine(root, "templates");
        var itemsDirectory = Path.Combine(templatesDirectory, "items");

        try
        {
            await loader.LoadAsync();

            Assert.Equal(49, Directory.GetFiles(itemsDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Equal(1664, service.Count);
            Assert.Empty(Directory.GetDirectories(templatesDirectory, ".items-*.tmp"));
            Assert.Contains(service.All, template => template.Weapon is not null && template.Equip is not null);
            Assert.Contains(service.All, template => template.Equip is not null && template.Equip.Layer != LayerType.None);
            Assert.Equal("items.training_dummy", service.GetById("training_dummy_east")!.ScriptId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenExistingDirectory_LoadsYamlRecursively()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "food.yaml", Item(id: "apple", name: "Apple"));
        WriteItem(root, "nested/weapons.yaml", Item(id: "sword", name: "Sword"));
        var service = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(service, directories).LoadAsync();

            Assert.Equal(2, service.Count);
            Assert.Equal("Apple", service.GetById("apple")!.Name);
            Assert.Equal("Sword", service.GetById("sword")!.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenExistingDirectoryIsEmpty_DoesNotSeed()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var itemsDirectory = Path.Combine(directories.RegisterDirectory("templates"), "items");
        Directory.CreateDirectory(itemsDirectory);
        var service = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(service, directories).LoadAsync();

            Assert.Empty(Directory.EnumerateFiles(itemsDirectory, "*", SearchOption.AllDirectories));
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_TargetAndMatchingStaleLegacy_FinalizesCleanup()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "target.yaml", Item(id: "target_item", name: "Target Item"));
        var dataDirectory = directories.RegisterDirectory("data");
        var legacyFile = Path.Combine(dataDirectory, "item_templates.yaml");
        var backupFile = legacyFile + ".migrated.bak";
        var legacyYaml = Item(id: "legacy_item", name: "Legacy Item");
        File.WriteAllText(legacyFile, legacyYaml);
        File.WriteAllText(backupFile, legacyYaml);
        var service = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(service, directories).LoadAsync();

            Assert.Equal(1, service.Count);
            Assert.Equal("Target Item", service.GetById("target_item")!.Name);
            Assert.Null(service.GetById("legacy_item"));
            Assert.False(File.Exists(legacyFile));
            Assert.True(File.Exists(backupFile));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_TargetAndDifferentStaleLegacy_LeavesBothFiles()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "target.yaml", Item(id: "target_item", name: "Target Item"));
        var dataDirectory = directories.RegisterDirectory("data");
        var legacyFile = Path.Combine(dataDirectory, "item_templates.yaml");
        var backupFile = legacyFile + ".migrated.bak";
        File.WriteAllText(legacyFile, Item(id: "legacy_item", name: "Legacy Item"));
        File.WriteAllText(backupFile, Item(id: "backup_item", name: "Backup Item"));
        var service = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(service, directories).LoadAsync();

            Assert.Equal(1, service.Count);
            Assert.NotNull(service.GetById("target_item"));
            Assert.True(File.Exists(legacyFile));
            Assert.True(File.Exists(backupFile));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_TargetAndLegacyWithoutBackup_LeavesLegacyUntouched()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "target.yaml", Item(id: "target_item", name: "Target Item"));
        var dataDirectory = directories.RegisterDirectory("data");
        var legacyFile = Path.Combine(dataDirectory, "item_templates.yaml");
        File.WriteAllText(legacyFile, Item(id: "legacy_item", name: "Legacy Item"));
        var service = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(service, directories).LoadAsync();

            Assert.Equal(1, service.Count);
            Assert.NotNull(service.GetById("target_item"));
            Assert.True(File.Exists(legacyFile));
            Assert.False(File.Exists(legacyFile + ".migrated.bak"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenSecondFileIsInvalid_RegistersNothing()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "a-valid.yaml", Item(id: "valid"));
        WriteItem(root, "b-invalid.yaml", Item(id: "invalid", itemId: -1));
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("b-invalid.yaml", exception.Message);
            Assert.Contains("invalid", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenDuplicateIdDiffersOnlyByCase_RegistersNothing()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "a.yaml", Item(id: "duplicate"));
        WriteItem(root, "b.yaml", Item(id: "DUPLICATE"));
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("b.yaml", exception.Message);
            Assert.Contains("DUPLICATE", exception.Message);
            Assert.Contains("Duplicate", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidYamlDocuments))]
    public async Task LoadAsync_WhenYamlSchemaIsInvalid_ReportsRelativePath(string yaml, string expectedMessage)
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "nested/schema.yaml", yaml);
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains(Path.Combine("nested", "schema.yaml"), exception.Message);
            Assert.NotNull(exception.InnerException);
            Assert.Contains(expectedMessage, exception.InnerException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [MemberData(nameof(IntrinsicallyInvalidDocuments))]
    public async Task LoadAsync_WhenIntrinsicValueIsNegative_ReportsTemplateAndProperty(
        string yaml,
        string templateId,
        string property
    )
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "validation.yaml", yaml);
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("validation.yaml", exception.Message);
            Assert.Contains(templateId, exception.Message);
            Assert.Contains(property, exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(".nan")]
    [InlineData(".inf")]
    [InlineData(".Inf")]
    [InlineData(".INF")]
    [InlineData("+.inf")]
    [InlineData("-.inf")]
    public async Task LoadAsync_WhenWeightIsNonFinite_RegistersNothing(string weight)
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "non-finite-weight.yaml", Item(extra: $"  Weight: {weight}\n"));
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("non-finite-weight.yaml", exception.Message);
            Assert.Contains("'item'", exception.Message);
            Assert.Contains("Weight", exception.Message);
            Assert.Contains("finite", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenSecondParamsValueIsNull_ReportsKeyWithoutFakeIndex()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(
            root,
            "params.yaml",
            Item(
                extra: "  Params:\n" +
                       "    valid:\n" +
                       "      Type: string\n" +
                       "      Value: value\n" +
                       "    broken: null\n"
            )
        );
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("params.yaml", exception.Message);
            Assert.Contains("Params[broken]", exception.Message);
            Assert.DoesNotContain("value at index", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [MemberData(nameof(StrictNullDocuments))]
    public async Task LoadAsync_WhenGuardedPropertyIsNull_ReportsPropertyAndLeavesRegistryEmpty(
        string yaml,
        string property
    )
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "strict-null.yaml", yaml);
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("strict-null.yaml", exception.Message);
            Assert.Contains("'item'", exception.Message);
            Assert.Contains(property, exception.Message);
            Assert.Contains("null", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCollectionDocuments))]
    public async Task LoadAsync_WhenCollectionContainsNull_ReportsCollectionAndIndex(
        string yaml,
        string property
    )
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteItem(root, "collections.yaml", yaml);
        var service = new ItemTemplateService();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await new ItemTemplatesLoader(service, directories).LoadAsync()
            );

            Assert.Contains("collections.yaml", exception.Message);
            Assert.Contains("'item'", exception.Message);
            Assert.Contains(property, exception.Message);
            Assert.Contains("index 0", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    public static TheoryData<string, string> InvalidYamlDocuments()
    {
        return new TheoryData<string, string>
        {
            { "- Id: broken\n  Tags: [", "expected" },
            { Item() + "  UnknownProperty: true\n", "UnknownProperty" },
            { Item().Replace("  Name: Item\n", "  Name: Item\n  Name: Duplicate\n", StringComparison.Ordinal), "duplicate" },
            { string.Empty, "empty" },
            { "null\n", "null" },
            { "- null\n", "template element" },
            { Item(tags: "null"), "Tags" },
            { Item(tags: "  - null\n"), "Tags" },
            { Item(rarity: "42"), "Rarity" },
            { Item(extra: "  Equip:\n    Layer: 255\n"), "Layer" },
            { Item(itemId: null), "ItemId" },
            { Item(extra: "  Equip:\n    Layer: null\n"), "Layer" },
            { Item(extra: "  Weapon:\n    LowDamage: null\n"), "LowDamage" }
        };
    }

    public static TheoryData<string, string, string> IntrinsicallyInvalidDocuments()
    {
        return new TheoryData<string, string, string>
        {
            { Item(id: "\" \""), "<unknown>", "Id" },
            { Item(name: "\" \""), "item", "Name" },
            { Item(category: "\" \""), "item", "Category" },
            { Item(itemId: -1), "item", "ItemId" },
            { Item(extra: "  Hue: -1\n"), "item", "Hue" },
            { Item(extra: "  GoldValue: -1\n"), "item", "GoldValue" },
            { Item(extra: "  Weight: -1\n"), "item", "Weight" },
            { Item(extra: "  FlippableItemIds: [-1]\n"), "item", "FlippableItemIds[0]" },
            { Item(extra: "  Equip:\n    HitPoints: -1\n"), "item", "Equip.HitPoints" },
            { Item(extra: "  Equip:\n    StrengthReq: -1\n"), "item", "Equip.StrengthReq" },
            { Item(extra: "  Equip:\n    DexterityReq: -1\n"), "item", "Equip.DexterityReq" },
            { Item(extra: "  Equip:\n    IntelligenceReq: -1\n"), "item", "Equip.IntelligenceReq" },
            { Item(extra: WeaponWith("LowDamage", -1)), "item", "Weapon.LowDamage" },
            { Item(extra: WeaponWith("HighDamage", -1)), "item", "Weapon.HighDamage" },
            { Item(extra: WeaponWith("Speed", -1)), "item", "Weapon.Speed" },
            { Item(extra: WeaponWith("BaseRange", -1)), "item", "Weapon.BaseRange" },
            { Item(extra: WeaponWith("MaxRange", -1)), "item", "Weapon.MaxRange" },
            { Item(extra: WeaponWith("HitSound", -1)), "item", "Weapon.HitSound" },
            { Item(extra: WeaponWith("MissSound", -1)), "item", "Weapon.MissSound" },
            { Item(extra: WeaponWith("Ammo", -1)), "item", "Weapon.Ammo" },
            { Item(extra: WeaponWith("AmmoFx", -1)), "item", "Weapon.AmmoFx" },
            { Item(extra: ContainerWith("WeightMax", -1)), "item", "Container.WeightMax" },
            { Item(extra: ContainerWith("MaxItems", -1)), "item", "Container.MaxItems" },
            { Item(extra: ContainerWith("GumpId", -1)), "item", "Container.GumpId" },
            { Item(extra: ContainerWith("WeightReduction", -1)), "item", "Container.WeightReduction" },
            { Item(extra: ContainerWith("QuiverDamageIncrease", -1)), "item", "Container.QuiverDamageIncrease" },
            { Item(extra: ContainerWith("LowerAmmoCost", -1)), "item", "Container.LowerAmmoCost" },
            { Item(extra: ContainerWith("DefenseChanceIncrease", -1)), "item", "Container.DefenseChanceIncrease" }
        };
    }

    public static TheoryData<string, string> InvalidCollectionDocuments()
    {
        return new TheoryData<string, string>
        {
            { Item(tags: "  - null\n"), "Tags" },
            { Item(extra: "  LootTables:\n  - null\n"), "LootTables" },
            { Item(extra: "  Container:\n    Contents:\n    - null\n"), "Container.Contents" }
        };
    }

    public static TheoryData<string, string> StrictNullDocuments()
    {
        return new TheoryData<string, string>
        {
            { Item(itemId: null), "ItemId" },
            { Item(extra: "  Hue: null\n"), "Hue" },
            { Item(extra: "  GoldValue: null\n"), "GoldValue" },
            { Item(extra: "  Weight: null\n"), "Weight" },
            { Item(extra: "  IsMovable: null\n"), "IsMovable" },
            { Item(rarity: "null"), "Rarity" },
            { Item(extra: "  Equip:\n    Layer: null\n"), "Equip.Layer" },
            { Item(extra: "  Weapon:\n    LowDamage: null\n"), "Weapon.LowDamage" },
            { Item(extra: "  Weapon:\n    HighDamage: null\n"), "Weapon.HighDamage" },
            { Item(extra: "  Weapon:\n    Speed: null\n"), "Weapon.Speed" },
            { Item(extra: "  Weapon:\n    BaseRange: null\n"), "Weapon.BaseRange" },
            { Item(extra: "  Weapon:\n    MaxRange: null\n"), "Weapon.MaxRange" },
            { Item(extra: "  Weapon:\n    HitSound: null\n"), "Weapon.HitSound" },
            { Item(extra: "  Weapon:\n    MissSound: null\n"), "Weapon.MissSound" },
            { Item(extra: "  FlippableItemIds: [null]\n"), "FlippableItemIds" }
        };
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-items-" + Guid.NewGuid().ToString("N"));

    private static void WriteItem(string root, string relativePath, string yaml)
    {
        var path = Path.Combine(root, "templates", "items", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml);
    }

    private static string Item(
        string id = "item",
        string name = "Item",
        string category = "Test",
        int? itemId = 1,
        string rarity = "Common",
        string tags = "[]",
        string extra = ""
    )
    {
        var yaml = $"- Id: {id}\n" +
                   $"  Name: {name}\n" +
                   $"  Category: {category}\n" +
                   $"  ItemId: {itemId?.ToString() ?? "null"}\n" +
                   $"  Rarity: {rarity}\n";

        yaml += tags == "[]" || tags == "null" ? $"  Tags: {tags}\n" : "  Tags:\n" + tags;
        return yaml + extra;
    }

    private static string WeaponWith(string property, int value)
    {
        return $"  Weapon:\n    {property}: {value}\n";
    }

    private static string ContainerWith(string property, int value)
    {
        return $"  Container:\n    {property}: {value}\n";
    }
}
