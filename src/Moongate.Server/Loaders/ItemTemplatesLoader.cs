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
        else
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
        var legacyExists = File.Exists(legacyFile);
        var hiddenClaims = GetOwnedLegacyClaims(legacyFile);

        if (!legacyExists && hiddenClaims.Length == 0)
        {
            return;
        }

        if (hiddenClaims.Length > 1 || (legacyExists && hiddenClaims.Length > 0))
        {
            _logger.Warning(
                "Ambiguous item template migration cleanup state for {LegacyPath}: " +
                "canonical legacy exists={LegacyExists}, hidden claim count={ClaimCount}. " +
                "Leaving all files and continuing with administrator-owned target {TargetPath}",
                legacyFile,
                legacyExists,
                hiddenClaims.Length,
                itemsDirectory
            );
            return;
        }

        if (!File.Exists(backupFile))
        {
            WarnUnsafeRecovery(legacyFile, itemsDirectory);
            return;
        }

        (string File, FileStream Lease)? backupSnapshot = null;
        (FileStream Stream, UnixFileMode? UnixMode)? backupLease = null;
        (FileStream Stream, UnixFileMode? UnixMode)? claimedLease = null;
        string? claimedLegacy = null;

        try
        {
            backupLease = ItemTemplateDirectoryMigrator.OpenProtectedReadLease(
                backupFile,
                allowDelete: false
            );
            backupSnapshot = ItemTemplateDirectoryMigrator.CreateImmutableSnapshot(
                backupFile,
                "recovery-snapshot"
            );

            if (!ItemTemplateDirectoryMigrator.StreamsEqual(
                    backupLease.Value.Stream,
                    backupSnapshot.Value.Lease
                ))
            {
                WarnUnsafeRecovery(legacyFile, itemsDirectory);
                return;
            }

            if (!legacyExists)
            {
                claimedLegacy = hiddenClaims[0];
                claimedLease = ItemTemplateDirectoryMigrator.OpenProtectedReadLease(
                    claimedLegacy,
                    allowDelete: true
                );

                if (ItemTemplateDirectoryMigrator.StreamsEqual(
                        claimedLease.Value.Stream,
                        backupSnapshot.Value.Lease
                    ))
                {
                    RecoveryCheckpoint("BeforeRecoveryDelete", claimedLegacy);
                    File.Delete(claimedLegacy);
                    ReleaseRecoveryLease(
                        claimedLegacy,
                        claimedLease.Value
                    );
                    claimedLease = null;
                    claimedLegacy = null;
                    _logger.Information(
                        "Removed verified hidden item template legacy claim {ClaimPath}; target is {TargetPath}",
                        hiddenClaims[0],
                        itemsDirectory
                    );
                    return;
                }

                ReleaseRecoveryLease(
                    claimedLegacy,
                    claimedLease.Value
                );
                claimedLease = null;
                var restored = TryRestoreRecoveryClaim(claimedLegacy, legacyFile);
                WarnUnsafeRecovery(legacyFile, itemsDirectory, restored ? null : claimedLegacy);
                claimedLegacy = restored ? null : claimedLegacy;
                return;
            }

            RecoveryCheckpoint("BeforeRecoveryComparison", legacyFile);

            if (!ItemTemplateDirectoryMigrator.FilesEqual(legacyFile, backupSnapshot.Value.File))
            {
                WarnUnsafeRecovery(legacyFile, itemsDirectory);
                return;
            }

            claimedLegacy = ItemTemplateDirectoryMigrator.CreateLegacyClaimPath(legacyFile);
            RecoveryCheckpoint("BeforeRecoveryLegacyClaim", legacyFile);
            File.Move(legacyFile, claimedLegacy, overwrite: false);
            claimedLease = ItemTemplateDirectoryMigrator.OpenProtectedReadLease(
                claimedLegacy,
                allowDelete: true
            );

            if (!ItemTemplateDirectoryMigrator.StreamsEqual(
                    claimedLease.Value.Stream,
                    backupSnapshot.Value.Lease
                ))
            {
                ReleaseRecoveryLease(
                    claimedLegacy,
                    claimedLease.Value
                );
                claimedLease = null;
                var restored = TryRestoreRecoveryClaim(claimedLegacy, legacyFile);
                WarnUnsafeRecovery(legacyFile, itemsDirectory, restored ? null : claimedLegacy);
                claimedLegacy = restored ? null : claimedLegacy;
                return;
            }

            RecoveryCheckpoint("BeforeRecoveryDelete", claimedLegacy);
            File.Delete(claimedLegacy);
            ReleaseRecoveryLease(
                claimedLegacy,
                claimedLease.Value
            );
            claimedLease = null;
            claimedLegacy = null;
            _logger.Information(
                "Finalized item template migration cleanup for {LegacyPath}; target is {TargetPath}",
                legacyFile,
                itemsDirectory
            );
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            if (claimedLease is not null && claimedLegacy is not null)
            {
                ReleaseRecoveryLease(
                    claimedLegacy,
                    claimedLease.Value
                );
                claimedLease = null;
            }

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
            if (claimedLease is not null && claimedLegacy is not null)
            {
                ReleaseRecoveryLease(
                    claimedLegacy,
                    claimedLease.Value
                );
            }

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

            if (backupLease is not null)
            {
                ReleaseRecoveryLease(
                    backupFile,
                    backupLease.Value
                );
            }
        }
    }

    private static string[] GetOwnedLegacyClaims(string legacyFile)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(legacyFile))!;
        var fileName = Path.GetFileName(legacyFile);
        var purposes = new[] { "legacy-claim", "delete-claim", "recovery-claim" };

        return purposes
            .SelectMany(
                purpose => Directory.GetFiles(
                    directory,
                    $".{fileName}-*.{purpose}.tmp",
                    SearchOption.TopDirectoryOnly
                )
            )
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ReleaseRecoveryLease(
        string file,
        (FileStream Stream, UnixFileMode? UnixMode) lease
    )
    {
        try
        {
            ItemTemplateDirectoryMigrator.ReleaseProtectedReadLease(file, lease);
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            _logger.Warning(
                exception,
                "Unable to restore file mode after item template recovery lease for {Path}",
                file
            );
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
