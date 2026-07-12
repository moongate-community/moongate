using Moongate.Server.Data.Internal;
using Moongate.Server.Interfaces;
using Moongate.Server.Internal;
using Moongate.Server.Services;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Server.Loaders;

/// <summary>Seeds and loads item templates from the recursive item template directory.</summary>
public sealed class ItemTemplatesLoader : IDataLoader
{
    private const string EmbeddedDirectory = "Assets/Templates/Items";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Templates.Items";

    private static Action<string, string>? _recoveryCheckpointForTests = null;

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
                "into {FileCount} YAML file(s); backup retained at {BackupPath}",
                migrationResult.StandardCount,
                migrationResult.CustomCount,
                migrationResult.FileCount,
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

        (string File, FileStream Lease)? backupSnapshot = null;
        string? claimedLegacy = null;

        try
        {
            backupSnapshot = ItemTemplateDirectoryMigrator.CreateImmutableSnapshot(
                backupFile,
                "recovery-snapshot"
            );

            RecoveryCheckpoint("BeforeRecoveryComparison", legacyFile);

            if (!ItemTemplateDirectoryMigrator.FilesEqual(legacyFile, backupSnapshot.Value.File))
            {
                WarnUnsafeRecovery(legacyFile, itemsDirectory);
                return;
            }

            claimedLegacy = ItemTemplateDirectoryMigrator.CreateOwnedSiblingPath(
                legacyFile,
                "recovery-claim"
            );
            RecoveryCheckpoint("BeforeRecoveryLegacyClaim", legacyFile);
            File.Move(legacyFile, claimedLegacy, overwrite: false);

            if (!ItemTemplateDirectoryMigrator.FilesEqual(claimedLegacy, backupSnapshot.Value.File))
            {
                var restored = TryRestoreRecoveryClaim(claimedLegacy, legacyFile);
                WarnUnsafeRecovery(legacyFile, itemsDirectory, restored ? null : claimedLegacy);
                claimedLegacy = restored ? null : claimedLegacy;
                return;
            }

            RecoveryCheckpoint("BeforeRecoveryDelete", claimedLegacy);
            File.Delete(claimedLegacy);
            claimedLegacy = null;
            _logger.Information(
                "Finalized item template migration cleanup for {LegacyPath}; target is {TargetPath}",
                legacyFile,
                itemsDirectory
            );
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            var restored = TryRestoreRecoveryClaim(claimedLegacy, legacyFile);
            _logger.Warning(
                exception,
                "Unable to finalize item template migration cleanup for {LegacyPath}; " +
                "continuing with administrator-owned target {TargetPath}. Legacy claim retained at {ClaimPath}",
                legacyFile,
                itemsDirectory,
                restored ? null : claimedLegacy
            );
        }
        finally
        {
            if (backupSnapshot is not null)
            {
                backupSnapshot.Value.Lease.Dispose();

                try
                {
                    File.Delete(backupSnapshot.Value.File);
                }
                catch (Exception exception) when (IsFileIoFailure(exception))
                {
                    _logger.Warning(
                        exception,
                        "Unable to remove owned item template recovery snapshot {SnapshotPath}",
                        backupSnapshot.Value.File
                    );
                }
            }
        }
    }

    private void WarnUnsafeRecovery(string legacyFile, string itemsDirectory, string? claimPath = null)
    {
        _logger.Warning(
            "Item template target {TargetPath} already exists; leaving legacy source {LegacyPath} " +
            "because no byte-identical migration backup snapshot can be safely claimed. " +
            "Legacy claim retained at {ClaimPath}",
            itemsDirectory,
            legacyFile,
            claimPath
        );
    }

    private static bool TryRestoreRecoveryClaim(string? claimedFile, string legacyFile)
    {
        if (claimedFile is null || !File.Exists(claimedFile))
        {
            return true;
        }

        if (File.Exists(legacyFile))
        {
            return false;
        }

        try
        {
            File.Move(claimedFile, legacyFile, overwrite: false);
            return true;
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            return false;
        }
    }

    private static bool IsFileIoFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static void RecoveryCheckpoint(string checkpoint, string path)
    {
        _recoveryCheckpointForTests?.Invoke(checkpoint, path);
    }
}
