using System.Reflection;
using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class LootTemplatesLoaderTests
{
    [Fact]
    public void EmbeddedResourceDirectorySeeder_SeedAtomic_WhenDestinationCollides_CleansDerivedTemporaryDirectory()
    {
        var root = NewRoot();
        var destinationDirectory = Path.Combine(root, "archive");
        const string collisionContents = "destination-directory-collision";

        Directory.CreateDirectory(root);
        File.WriteAllText(destinationDirectory, collisionContents);

        try
        {
            var seederType = typeof(LootTemplatesLoader).Assembly.GetType(
                "Moongate.Server.Internal.EmbeddedResourceDirectorySeeder"
            );
            Assert.NotNull(seederType);

            var seedAtomic = seederType.GetMethod("SeedAtomic");
            Assert.NotNull(seedAtomic);

            var exception = Assert.Throws<TargetInvocationException>(
                () => seedAtomic.Invoke(
                    null,
                    [
                        typeof(LootTemplatesLoader).Assembly,
                        "Assets/Templates/Loot",
                        "Moongate.Server.Assets.Templates.Loot",
                        destinationDirectory
                    ]
                )
            );

            Assert.IsType<IOException>(exception.InnerException);
            Assert.True(File.Exists(destinationDirectory));
            Assert.Equal(collisionContents, File.ReadAllText(destinationDirectory));
            Assert.Empty(Directory.GetDirectories(root, ".archive-*.tmp"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    public static TheoryData<string> ExplicitNullNonNullableYamlDocuments()
        => new()
        {
            Loot(mode: "null"),
            Loot().Replace("NoDropWeight: 0", "NoDropWeight: null", StringComparison.Ordinal),
            Loot(description: "null"),
            Loot().Replace("Rolls: 1", "Rolls: null", StringComparison.Ordinal)
        };

    public static TheoryData<string, string, string, int?> InvalidDocuments()
        => new()
        {
            { Loot("\" \""), "Id is required", "<unknown>", null },
            { Loot(name: "\" \""), "Name is required", "loot", null },
            { Loot(category: "\" \""), "Category is required", "loot", null },
            {
                Loot("duplicate") + Loot("DUPLICATE"),
                "Duplicate loot template ID",
                "DUPLICATE",
                null
            },
            { Loot(entries: "[]"), "must contain at least one entry", "loot", null },
            { Loot(rolls: 0), "Rolls must be greater than zero", "loot", null },
            { Loot(noDropWeight: -1), "NoDropWeight cannot be negative", "loot", null },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "ItemTag: weapon", "Weight: 1", "Amount: 1")),
                "exactly one item reference",
                "loot",
                0
            },
            { Loot(entries: Entry("Weight: 1", "Amount: 1")), "exactly one item reference", "loot", 0 },
            {
                Loot(entries: Entry("ItemTemplateId: missing_item", "Weight: 1", "Amount: 1")), "Unknown ItemTemplateId",
                "loot", 0
            },
            { Loot(entries: Entry("ItemTag: missing_tag", "Weight: 1", "Amount: 1")), "Unknown ItemTag", "loot", 0 },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1")), "fixed Amount or an amount range", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "Amount: 1", "AmountMin: 1", "AmountMax: 2")),
                "cannot combine Amount with a range", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "AmountMin: 1")),
                "requires both AmountMin and AmountMax", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "AmountMin: 2", "AmountMax: 1")),
                "AmountMin cannot exceed AmountMax", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "Amount: 0")),
                "Amount must be greater than zero", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "Amount: -1")),
                "Amount must be greater than zero", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "AmountMin: 0", "AmountMax: 1")),
                "Amount range bounds must be greater than zero", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "AmountMin: 1", "AmountMax: 0")),
                "Amount range bounds must be greater than zero", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 0", "Amount: 1")),
                "Weight must be greater than zero", "loot", 0
            },
            {
                Loot(entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "Chance: 0.5", "Amount: 1")),
                "Weighted entry cannot define Chance", "loot", 0
            },
            {
                Loot(mode: "Additive", entries: Entry("ItemTemplateId: broadsword", "Amount: 1")),
                "Additive entry requires Chance", "loot", 0
            },
            {
                Loot(mode: "Additive", entries: Entry("ItemTemplateId: broadsword", "Chance: 1.1", "Amount: 1")),
                "Chance must be between 0 and 1", "loot", 0
            },
            {
                Loot(mode: "Additive", entries: Entry("ItemTemplateId: broadsword", "Chance: .nan", "Amount: 1")),
                "Chance must be finite", "loot", 0
            },
            {
                Loot(mode: "Additive", entries: Entry("ItemTemplateId: broadsword", "Chance: .inf", "Amount: 1")),
                "Chance must be finite", "loot", 0
            },
            {
                Loot(mode: "Additive", entries: Entry("ItemTemplateId: broadsword", "Chance: -.inf", "Amount: 1")),
                "Chance must be finite", "loot", 0
            },
            {
                Loot(
                    mode: "Additive",
                    entries: Entry("ItemTemplateId: broadsword", "Weight: 1", "Chance: 0.5", "Amount: 1")
                ),
                "Additive entry cannot define Weight", "loot", 0
            }
        };

    public static TheoryData<string, string> InvalidYamlDocuments()
        => new()
        {
            { Loot() + "  UnknownProperty: true\n", "UnknownProperty" },
            { Loot().Replace("  Name: Loot\n", "  Name: Loot\n  Name: Duplicate\n", StringComparison.Ordinal), "duplicate" },
            { "", "empty" },
            { "null\n", "null" },
            { "- null\n", "template element" },
            { Loot(entries: "null"), "null" },
            { Loot(entries: "  - null\n"), "entry element" },
            { Loot(mode: "42"), "Mode" }
        };

    [Theory, InlineData("0"), InlineData("1")]
    public async Task LoadAsync_AdditiveChanceBoundary_Loads(string chance)
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(
            root,
            "chance-boundary.yaml",
            Loot(mode: "Additive", entries: Entry("ItemTemplateId: broadsword", $"Chance: {chance}", "Amount: 1"))
        );
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(double.Parse(chance), service.GetById("loot")!.Entries.Single().Chance);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_ExistingDirectory_LoadsYamlRecursively()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(root, "weapons.yaml", Loot("weapon_pack"));
        WriteLoot(
            root,
            "reagents/additive.yaml",
            Loot(
                "reagent_pack",
                mode: "Additive",
                entries: Entry("ItemTag: reagent", "Chance: 0.5", "AmountMin: 2", "AmountMax: 5")
            )
        );
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(2, service.Count);
            Assert.NotNull(service.GetById("weapon_pack"));
            Assert.NotNull(service.GetById("reagent_pack"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_ExistingEmptyDirectory_DoesNotSeedDefaults()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var lootDirectory = Path.Combine(directories.RegisterDirectory("templates"), "loot");
        Directory.CreateDirectory(lootDirectory);
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            await loader.LoadAsync();

            Assert.Empty(Directory.EnumerateFiles(lootDirectory, "*", SearchOption.AllDirectories));
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory, MemberData(nameof(ExplicitNullNonNullableYamlDocuments))]
    public async Task LoadAsync_ExplicitNullForNonNullableScalar_WrapsDeserializationFailureWithRelativePath(string yaml)
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(root, "nested/explicit-null.yaml", yaml);
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () => await loader.LoadAsync());

            Assert.Contains(Path.Combine("nested", "explicit-null.yaml"), exception.Message);
            Assert.NotNull(exception.InnerException);
            Assert.Contains("null", exception.InnerException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory, MemberData(nameof(InvalidDocuments))]
    public async Task LoadAsync_InvalidDocument_ReportsPreciseValidationFailure(
        string yaml,
        string expectedMessage,
        string expectedTemplateId,
        int? expectedEntryIndex
    )
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(root, "matrix.yaml", yaml);
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () => await loader.LoadAsync());

            Assert.Contains("matrix.yaml", exception.Message);
            Assert.Contains(expectedTemplateId, exception.Message);
            Assert.Contains(expectedMessage, exception.Message);

            if (expectedEntryIndex.HasValue)
            {
                Assert.Contains($"entry {expectedEntryIndex.Value}", exception.Message);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_InvalidSecondFile_RegistersNothing()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(root, "a-valid.yaml", Loot("valid_pack"));
        WriteLoot(
            root,
            "b-invalid.yaml",
            Loot("invalid_pack", entries: Entry("ItemTemplateId: missing_item", "Weight: 1", "Amount: 1"))
        );
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () => await loader.LoadAsync());

            Assert.Contains("b-invalid.yaml", exception.Message);
            Assert.Contains("invalid_pack", exception.Message);
            Assert.Contains("entry 0", exception.Message);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory, MemberData(nameof(InvalidYamlDocuments))]
    public async Task LoadAsync_InvalidYamlSchema_WrapsFailureWithRelativePath(string yaml, string expectedMessage)
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(root, "nested/schema.yaml", yaml);
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () => await loader.LoadAsync());

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

    [Fact]
    public async Task LoadAsync_MalformedYaml_ReportsRelativePath()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        WriteLoot(root, "nested/malformed.yaml", "- Id: broken\n  Entries: [");
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(async () => await loader.LoadAsync());

            Assert.Contains(Path.Combine("nested", "malformed.yaml"), exception.Message);
            Assert.NotNull(exception.InnerException);
            Assert.Equal(0, service.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReferencesResolveCaseInsensitively()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var yaml = Loot(
                       "id_reference",
                       entries: Entry("ItemTemplateId: BROADSWORD", "Weight: 1", "Amount: 1")
                   ) +
                   Loot(
                       "tag_reference",
                       entries: Entry("ItemTag: REAGENT", "Weight: 1", "Amount: 1")
                   );
        WriteLoot(root, "case-insensitive.yaml", yaml);
        var service = new LootTemplateService();
        var loader = new LootTemplatesLoader(service, CreateItems(), directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(2, service.Count);
            Assert.NotNull(service.GetById("id_reference"));
            Assert.NotNull(service.GetById("tag_reference"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_SeedDestinationCollision_CleansTemporaryDirectoryAndLeavesRegistryUnchanged()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var templatesDirectory = directories.RegisterDirectory("templates");
        var lootDirectory = Path.Combine(templatesDirectory, "loot");
        var collisionContents = "loot-directory-collision";
        var lootTemplates = new LootTemplateService();
        var loader = new LootTemplatesLoader(lootTemplates, CreateItems(), directories);

        File.WriteAllText(lootDirectory, collisionContents);

        try
        {
            await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync());

            Assert.True(File.Exists(lootDirectory));
            Assert.Equal(collisionContents, File.ReadAllText(lootDirectory));
            Assert.Empty(Directory.GetDirectories(templatesDirectory, ".loot-*.tmp"));
            Assert.Equal(0, lootTemplates.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsCompleteDataset()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var itemTemplates = new ItemTemplateService();
        var lootTemplates = new LootTemplateService();
        var lootDirectory = Path.Combine(root, "templates", "loot");

        try
        {
            await new ItemTemplatesLoader(itemTemplates, directories).LoadAsync();
            await new LootTemplatesLoader(lootTemplates, itemTemplates, directories).LoadAsync();

            Assert.Equal(140, Directory.GetFiles(lootDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(lootDirectory)!, ".loot-*.tmp"));
            Assert.Equal(279, lootTemplates.Count);
            Assert.Equal(
                279,
                lootTemplates.All.Select(template => template.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            );
            Assert.NotNull(lootTemplates.GetById("creature.balron"));

            var weighted = lootTemplates.GetById("fillable.alchemist")!;
            Assert.Equal(LootTemplateModeType.Weighted, weighted.Mode);
            Assert.Equal(9, weighted.Entries.Count);
            Assert.Equal("night_sight_potion", weighted.Entries[0].ItemTemplateId);
            Assert.Equal(1, weighted.Entries[0].Weight);
            Assert.Equal(1, weighted.Entries[0].Amount);

            var additive = lootTemplates.GetById("creature.balron")!;
            Assert.Equal(LootTemplateModeType.Additive, additive.Mode);
            Assert.Equal("longsword", additive.Entries[0].ItemTemplateId);
            Assert.Equal(1d, additive.Entries[0].Chance);
            Assert.Equal(1, additive.Entries[0].Amount);

            var tagged = lootTemplates.GetById("creature.bone_magi")!;
            Assert.Equal("reagents", tagged.Entries[1].ItemTag);
            Assert.Equal(1d, tagged.Entries[1].Chance);
            Assert.Equal(3, tagged.Entries[1].Amount);

            var ranged = lootTemplates.GetById("guard.warrior")!;
            Assert.Equal("gold", ranged.Entries[0].ItemTemplateId);
            Assert.Equal(10, ranged.Entries[0].AmountMin);
            Assert.Equal(25, ranged.Entries[0].AmountMax);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static ItemTemplateService CreateItems()
    {
        var items = new ItemTemplateService();
        items.Register(new() { Id = "broadsword", Tags = ["weapon"] });
        items.Register(new() { Id = "black_pearl", Tags = ["reagent"] });

        return items;
    }

    private static string Entry(params string[] properties)
        => "  - " + properties[0] + "\n" + string.Concat(properties.Skip(1).Select(property => "    " + property + "\n"));

    private static string Loot(
        string id = "loot",
        string name = "Loot",
        string category = "Test",
        string description = "Description",
        string mode = "Weighted",
        int rolls = 1,
        int noDropWeight = 0,
        string? entries = null
    )
    {
        entries ??= Entry("ItemTemplateId: broadsword", "Weight: 1", "Amount: 1");

        var template = $"- Id: {id}\n" +
                       $"  Name: {name}\n" +
                       $"  Category: {category}\n" +
                       $"  Description: {description}\n" +
                       $"  Mode: {mode}\n" +
                       $"  Rolls: {rolls}\n" +
                       $"  NoDropWeight: {noDropWeight}\n";

        if (entries is "[]" or "null")
        {
            return template + $"  Entries: {entries}\n";
        }

        return template + "  Entries:\n" + entries;
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-loot-" + Guid.NewGuid().ToString("N"));

    private static void WriteLoot(string root, string relativePath, string yaml)
    {
        var path = Path.Combine(root, "templates", "loot", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml);
    }
}
