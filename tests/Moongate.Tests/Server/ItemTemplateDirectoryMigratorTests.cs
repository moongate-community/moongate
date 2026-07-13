using System.Reflection;
using System.Text;
using Moongate.Server.Data.Internal;
using Moongate.Server.Internal;
using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Items;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;
using YamlDotNet.Serialization;

namespace Moongate.Tests.Server;

[Collection("ItemTemplateMigration")]
public class ItemTemplateDirectoryMigratorTests
{
    private const string EmbeddedDirectory = "Assets/Templates/Items";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Templates.Items";

    [Fact]
    public async Task Migrate_CurrentLegacy_Creates49FilesAndBackup()
    {
        var paths = NewPaths();
        var legacyBytes = WriteLegacy(paths.LegacyFile, CurrentLegacyYaml());

        try
        {
            var result = Migrate(paths);

            Assert.Equal(1664, result.StandardCount);
            Assert.Equal(0, result.CustomCount);
            Assert.Equal(49, result.FileCount);
            Assert.Equal(paths.BackupFile, result.BackupPath);
            Assert.Equal(49, Directory.GetFiles(paths.TargetDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.False(File.Exists(paths.LegacyFile));
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.BackupFile));
            AssertNoTemporaryDirectory(paths.Root);
            AssertStandardFileMembership(paths.TargetDirectory);

            var service = await LoadMigratedTemplates(paths.Root);
            Assert.Equal(1664, service.Count);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public async Task Migrate_LegacyOverride_WinsOverEmbeddedDefault()
    {
        var paths = NewPaths();
        var legacyYaml = CurrentLegacyYaml();
        var overriddenYaml = legacyYaml.Replace(
            "- Id: apple\n  Name: Apple\n",
            "- Id: apple\n  Name: Legacy Apple Override\n",
            StringComparison.Ordinal
        );
        Assert.NotEqual(legacyYaml, overriddenYaml);
        WriteLegacy(paths.LegacyFile, overriddenYaml);

        try
        {
            Migrate(paths);

            var service = await LoadMigratedTemplates(paths.Root);
            Assert.Equal("Legacy Apple Override", service.GetById("apple")!.Name);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public async Task Migrate_MissingLegacyDefault_AddsEmbeddedDefinition()
    {
        var paths = NewPaths();
        WriteLegacy(paths.LegacyFile, RemoveTemplate(CurrentLegacyYaml(), "apple"));

        try
        {
            var result = Migrate(paths);

            var service = await LoadMigratedTemplates(paths.Root);
            Assert.Equal(1664, result.StandardCount);
            Assert.Equal(1664, service.Count);
            Assert.Equal("Apple", service.GetById("apple")!.Name);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public async Task Migrate_UnknownLegacyId_WritesCustomYamlOrderedById()
    {
        var paths = NewPaths();
        var legacyYaml = CurrentLegacyYaml() +
                         CustomItem("zeta_custom") +
                         CustomItem("Alpha_custom") +
                         CustomItem("beta_Custom");
        var legacyBytes = WriteLegacy(paths.LegacyFile, legacyYaml);

        try
        {
            var result = Migrate(paths);
            var customFile = Path.Combine(paths.TargetDirectory, "custom.yaml");
            var customTemplates = YamlUtils.DeserializeFromFile<ItemTemplate[]>(customFile)!;

            Assert.Equal(1664, result.StandardCount);
            Assert.Equal(3, result.CustomCount);
            Assert.Equal(50, result.FileCount);
            Assert.Equal(50, Directory.GetFiles(paths.TargetDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Equal(
                ["Alpha_custom", "beta_Custom", "zeta_custom"],
                customTemplates.Select(template => template.Id)
            );
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.BackupFile));
            Assert.False(File.Exists(paths.LegacyFile));

            var service = await LoadMigratedTemplates(paths.Root);
            Assert.Equal(1667, service.Count);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public void Migrate_MatchingExistingBackup_ReusesBackup()
    {
        var paths = NewPaths();
        var legacyBytes = WriteLegacy(paths.LegacyFile, CurrentLegacyYaml());
        File.WriteAllBytes(paths.BackupFile, legacyBytes);
        var backupTimestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(paths.BackupFile, backupTimestamp);

        try
        {
            Migrate(paths);

            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.BackupFile));
            Assert.Equal(backupTimestamp, File.GetLastWriteTimeUtc(paths.BackupFile));
            Assert.False(File.Exists(paths.LegacyFile));
            Assert.True(Directory.Exists(paths.TargetDirectory));
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public void Migrate_DifferentExistingBackup_FailsWithoutChanges()
    {
        var paths = NewPaths();
        var legacyBytes = WriteLegacy(paths.LegacyFile, CurrentLegacyYaml());
        var backupBytes = Encoding.UTF8.GetBytes("different-backup\n");
        File.WriteAllBytes(paths.BackupFile, backupBytes);

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => Migrate(paths));

            Assert.Equal(
                "Existing item template migration backup differs from legacy source.",
                exception.Message
            );
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.LegacyFile));
            Assert.Equal(backupBytes, File.ReadAllBytes(paths.BackupFile));
            Assert.False(Directory.Exists(paths.TargetDirectory));
            AssertNoTemporaryDirectory(paths.Root);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public void Migrate_InvalidLegacy_LeavesLegacyAndTargetUnchanged()
    {
        var paths = NewPaths();
        var invalidYaml = CurrentLegacyYaml() +
                          "- Id: apple\n" +
                          "  Name: Duplicate Apple\n" +
                          "  Category: Invalid\n" +
                          "  ItemId: 1\n" +
                          "  Rarity: Common\n" +
                          "  Tags: []\n";
        var legacyBytes = WriteLegacy(paths.LegacyFile, invalidYaml);

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => Migrate(paths));

            Assert.Contains("Duplicate item template ID 'apple'", exception.Message);
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.LegacyFile));
            Assert.False(File.Exists(paths.BackupFile));
            Assert.False(Directory.Exists(paths.TargetDirectory));
            AssertNoTemporaryDirectory(paths.Root);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public void Migrate_TargetMoveFailure_CleansTemporaryDirectory()
    {
        var paths = NewPaths();
        var legacyBytes = WriteLegacy(paths.LegacyFile, CurrentLegacyYaml());
        const string targetContents = "target-collision";
        Directory.CreateDirectory(Path.GetDirectoryName(paths.TargetDirectory)!);
        File.WriteAllText(paths.TargetDirectory, targetContents);

        try
        {
            Assert.Throws<IOException>(() => Migrate(paths));

            Assert.True(File.Exists(paths.TargetDirectory));
            Assert.Equal(targetContents, File.ReadAllText(paths.TargetDirectory));
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.LegacyFile));
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.BackupFile));
            AssertNoTemporaryDirectory(paths.Root);
        }
        finally
        {
            Directory.Delete(paths.Root, true);
        }
    }

    [Fact]
    public void Migrate_BackupPublishFailure_LeavesNoPartialBackupAndCleansOwnedTemps()
    {
        var paths = NewPaths();
        var legacyBytes = WriteLegacy(paths.LegacyFile, CurrentLegacyYaml());

        try
        {
            SetMigrationIoFailure(
                phase =>
                {
                    if (phase == "backup-publish")
                    {
                        throw new IOException("Injected backup publication failure.");
                    }
                }
            );

            var exception = Assert.Throws<IOException>(() => Migrate(paths));

            Assert.Equal("Injected backup publication failure.", exception.Message);
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.LegacyFile));
            Assert.False(File.Exists(paths.BackupFile));
            Assert.False(Directory.Exists(paths.TargetDirectory));
            AssertNoTemporaryDirectory(paths.Root);
            AssertNoOwnedDataTemporaryFiles(paths.Root);
        }
        finally
        {
            SetMigrationIoFailure(null);
            Directory.Delete(paths.Root, true);
        }
    }

    [Theory]
    [InlineData("legacy-compare")]
    [InlineData("legacy-delete")]
    public void Migrate_PostCommitLegacyCleanupIoFailure_LoadsCommittedTarget(string failingPhase)
    {
        var paths = NewPaths();
        var legacyBytes = WriteLegacy(paths.LegacyFile, CurrentLegacyYaml());

        try
        {
            SetMigrationIoFailure(
                phase =>
                {
                    if (phase == failingPhase)
                    {
                        throw new IOException($"Injected migration cleanup failure at {phase}.");
                    }
                }
            );

            Migrate(paths);

            Assert.True(Directory.Exists(paths.TargetDirectory));
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.LegacyFile));
            Assert.Equal(legacyBytes, File.ReadAllBytes(paths.BackupFile));
            AssertNoOwnedDataTemporaryFiles(paths.Root);
        }
        finally
        {
            SetMigrationIoFailure(null);
            Directory.Delete(paths.Root, true);
        }
    }

    private static ItemTemplateMigrationResult Migrate(
        (string Root, string LegacyFile, string BackupFile, string TargetDirectory) paths
    )
    {
        return ItemTemplateDirectoryMigrator.Migrate(
            typeof(ItemTemplatesLoader).Assembly,
            EmbeddedDirectory,
            EmbeddedNamespace,
            paths.LegacyFile,
            paths.BackupFile,
            paths.TargetDirectory
        );
    }

    private static async Task<ItemTemplateService> LoadMigratedTemplates(string root)
    {
        var service = new ItemTemplateService();
        await new ItemTemplatesLoader(
            service,
            new DirectoriesConfig(root, Array.Empty<string>())
        ).LoadAsync();
        return service;
    }

    private static (string Root, string LegacyFile, string BackupFile, string TargetDirectory) NewPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-item-migration-" + Guid.NewGuid().ToString("N"));
        var legacyFile = Path.Combine(root, "data", "item_templates.yaml");
        return (
            root,
            legacyFile,
            legacyFile + ".migrated.bak",
            Path.Combine(root, "templates", "items")
        );
    }

    private static byte[] WriteLegacy(string legacyFile, string yaml)
    {
        var bytes = Encoding.UTF8.GetBytes(yaml);
        Directory.CreateDirectory(Path.GetDirectoryName(legacyFile)!);
        File.WriteAllBytes(legacyFile, bytes);
        return bytes;
    }

    private static string CurrentLegacyYaml()
    {
        var assembly = typeof(ItemTemplatesLoader).Assembly;
        var builder = new StringBuilder();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(EmbeddedNamespace + ".", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var yaml = reader.ReadToEnd();
            builder.Append(yaml);

            if (!yaml.EndsWith('\n'))
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string RemoveTemplate(string yaml, string id)
    {
        var marker = $"- Id: {id}\n";
        var start = yaml.IndexOf(marker, StringComparison.Ordinal);

        if (start < 0)
        {
            throw new InvalidOperationException($"Template '{id}' was not found in the legacy fixture.");
        }

        var next = yaml.IndexOf("\n- Id: ", start + marker.Length, StringComparison.Ordinal);
        var end = next < 0 ? yaml.Length : next + 1;
        return yaml.Remove(start, end - start);
    }

    private static string CustomItem(string id)
    {
        return $"- Id: {id}\n" +
               $"  Name: {id}\n" +
               "  Category: Custom\n" +
               "  ItemId: 1\n" +
               "  Rarity: Common\n" +
               "  Tags: []\n";
    }

    private static void AssertNoTemporaryDirectory(string root)
    {
        var templatesDirectory = Path.Combine(root, "templates");

        if (Directory.Exists(templatesDirectory))
        {
            Assert.Empty(Directory.GetDirectories(templatesDirectory, ".items-*.tmp"));
        }
    }

    private static void AssertNoOwnedDataTemporaryFiles(string root)
    {
        var dataDirectory = Path.Combine(root, "data");

        if (Directory.Exists(dataDirectory))
        {
            Assert.Empty(Directory.GetFiles(dataDirectory, ".*.tmp"));
        }
    }

    private static void AssertStandardFileMembership(string targetDirectory)
    {
        var assembly = typeof(ItemTemplatesLoader).Assembly;
        var deserializer = new DeserializerBuilder().Build();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(EmbeddedNamespace + ".", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(49, resourceNames.Length);

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var embedded = deserializer.Deserialize<ItemTemplate[]>(reader.ReadToEnd());
            var relativePath = ResourceUtils.ConvertResourceNameToPath(resourceName, EmbeddedNamespace);
            var generated = YamlUtils.DeserializeFromFile<ItemTemplate[]>(
                Path.Combine(targetDirectory, relativePath)
            )!;

            Assert.Equal(
                embedded.Select(template => template.Id),
                generated.Select(template => template.Id)
            );
        }
    }

    private static void SetMigrationIoFailure(Action<string>? failure)
    {
        var field = typeof(ItemTemplateDirectoryMigrator).GetField(
            "_ioFailureForTests",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        if (field is null)
        {
            if (failure is not null)
            {
                throw new InvalidOperationException("Migration I/O failure seam is missing.");
            }

            return;
        }

        field.SetValue(null, failure);
    }
}
