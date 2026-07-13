using Moongate.Server.Data.Internal;
using Moongate.Server.Interfaces;
using Moongate.Server.Internal;
using Moongate.Server.Services.Items;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Server.Loaders;

/// <summary>Seeds and loads item templates from the recursive item template directory.</summary>
public sealed class ItemTemplatesLoader : IDataLoader
{
    private const string EmbeddedDirectory = "Assets/Templates/Items";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Templates.Items";

    private static Action<string>? _recoveryIoFailureForTests = null;

    private readonly ILogger _logger = Log.ForContext<ItemTemplatesLoader>();
    private readonly IItemTemplateService _templates;
    private readonly DirectoriesConfig _directories;

    public ItemTemplatesLoader(IItemTemplateService templates, DirectoriesConfig directories)
    {
        _templates = templates;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var dataDirectory = _directories.RegisterDirectory("data");
        var legacyFile = Path.Combine(dataDirectory, "item_templates.yaml");
        var backupFile = legacyFile + ".migrated.bak";
        var templatesDirectory = _directories.RegisterDirectory("templates");
        var itemsDirectory = Path.Combine(templatesDirectory, "items");
        var directoryExists = Directory.Exists(itemsDirectory);

        if (!directoryExists && File.Exists(legacyFile))
        {
            var migrationResult = ItemTemplateDirectoryMigrator.Migrate(
                typeof(ItemTemplatesLoader).Assembly,
                EmbeddedDirectory,
                EmbeddedNamespace,
                legacyFile,
                backupFile,
                itemsDirectory
            );
            _logger.Information(
                "Migrated {StandardCount} standard and {CustomCount} custom item template(s) " +
                "from {LegacyPath} into {FileCount} YAML file(s) at {TargetPath}; backup retained at {BackupPath}",
                migrationResult.StandardCount,
                migrationResult.CustomCount,
                legacyFile,
                migrationResult.FileCount,
                itemsDirectory,
                migrationResult.BackupPath
            );
        }
        else if (!directoryExists)
        {
            EmbeddedResourceDirectorySeeder.SeedAtomic(
                typeof(ItemTemplatesLoader).Assembly,
                EmbeddedDirectory,
                EmbeddedNamespace,
                itemsDirectory
            );
        }
        else if (File.Exists(legacyFile))
        {
            FinalizeMigrationCleanup(legacyFile, backupFile, itemsDirectory);
        }

        var files = Directory.GetFiles(itemsDirectory, "*.yaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            if (directoryExists)
            {
                _logger.Warning("No item template YAML files found in existing directory {Path}", itemsDirectory);
            }

            return ValueTask.CompletedTask;
        }

        var sources = new List<ItemTemplateSource>();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(itemsDirectory, file);
            var templates = ItemTemplateYamlDeserializer.DeserializeFromFile(file, relativePath);
            sources.AddRange(templates.Select(template => new ItemTemplateSource(relativePath, template)));
        }

        ItemTemplateValidator.Validate(sources);

        foreach (var source in sources)
        {
            _templates.Register(source.Template);
        }

        _logger.Information(
            "Loaded {TemplateCount} item template(s) from {YamlFileCount} YAML file(s) in {Path}",
            sources.Count,
            files.Length,
            itemsDirectory
        );

        return ValueTask.CompletedTask;
    }

    private void FinalizeMigrationCleanup(string legacyFile, string backupFile, string itemsDirectory)
    {
        if (!File.Exists(backupFile))
        {
            WarnUnsafeRecovery(legacyFile, itemsDirectory);
            return;
        }

        try
        {
            InjectRecoveryIoFailure("compare");

            if (!ItemTemplateDirectoryMigrator.FilesEqual(legacyFile, backupFile))
            {
                WarnUnsafeRecovery(legacyFile, itemsDirectory);
                return;
            }

            InjectRecoveryIoFailure("delete");
            File.Delete(legacyFile);
            _logger.Information(
                "Finalized item template migration cleanup for {LegacyPath}; target is {TargetPath}",
                legacyFile,
                itemsDirectory
            );
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            _logger.Warning(
                exception,
                "Unable to finalize item template migration cleanup for {LegacyPath}; " +
                "continuing with administrator-owned target {TargetPath}",
                legacyFile,
                itemsDirectory
            );
        }
    }

    private void WarnUnsafeRecovery(string legacyFile, string itemsDirectory)
    {
        _logger.Warning(
            "Item template target {TargetPath} already exists; leaving legacy source {LegacyPath} " +
            "because no byte-identical migration backup is available",
            itemsDirectory,
            legacyFile
        );
    }

    private static bool IsFileIoFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static void InjectRecoveryIoFailure(string phase)
    {
        _recoveryIoFailureForTests?.Invoke(phase);
    }
}
