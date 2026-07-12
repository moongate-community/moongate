using System.Reflection;
using Moongate.Server.Data.Internal;
using Moongate.Server.Services;
using Moongate.UO.Data.Items;
using SquidStd.Core.Utils;

namespace Moongate.Server.Internal;

public static class ItemTemplateDirectoryMigrator
{
    private const int CopyBufferSize = 81920;

    private static Action<string, string>? _checkpointForTests = null;

    public static ItemTemplateMigrationResult Migrate(
        Assembly assembly,
        string embeddedDirectory,
        string embeddedNamespace,
        string legacyFile,
        string backupFile,
        string targetDirectory
    )
    {
        var normalizedTarget = Path.GetFullPath(targetDirectory);
        var parentDirectory = Path.GetDirectoryName(normalizedTarget)!;
        var directoryName = Path.GetFileName(normalizedTarget);
        var temporaryDirectory = Path.Combine(parentDirectory, $".{directoryName}-{Guid.NewGuid():N}.tmp");

        if (Directory.Exists(normalizedTarget))
        {
            throw new IOException($"Item template target directory '{normalizedTarget}' already exists.");
        }

        Directory.CreateDirectory(parentDirectory);

        var legacySnapshot = CreateImmutableSnapshot(legacyFile, "migration-snapshot");
        (FileStream Stream, UnixFileMode? UnixMode)? backupLease = null;

        try
        {
            var migrationData = BuildMigrationDirectory(
                assembly,
                embeddedDirectory,
                embeddedNamespace,
                legacyFile,
                legacySnapshot.File,
                normalizedTarget,
                temporaryDirectory
            );

            backupLease = PublishBackupAtomic(
                legacySnapshot.File,
                legacySnapshot.Lease,
                backupFile
            );
            Checkpoint("AfterBackupLeaseAcquired", backupFile);

            try
            {
                Directory.Move(temporaryDirectory, normalizedTarget);
            }
            catch
            {
                DeleteDirectoryIfExists(temporaryDirectory);
                throw;
            }

            DeleteLegacyIfSnapshotMatches(
                legacyFile,
                legacySnapshot.File,
                legacySnapshot.Lease
            );

            return new ItemTemplateMigrationResult(
                migrationData.StandardCount,
                migrationData.CustomCount,
                migrationData.FileCount,
                backupFile
            );
        }
        catch
        {
            DeleteDirectoryIfExists(temporaryDirectory);
            throw;
        }
        finally
        {
            if (backupLease is not null)
            {
                ReleaseProtectedReadLease(backupFile, backupLease.Value);
            }

            legacySnapshot.Lease.Dispose();
            File.Delete(legacySnapshot.File);
        }
    }

    internal static (string File, FileStream Lease) CreateImmutableSnapshot(string sourceFile, string purpose)
    {
        var snapshotFile = CreateOwnedSiblingPath(sourceFile, purpose);

        try
        {
            CopyToDurableFile(sourceFile, snapshotFile, FileShare.None).Dispose();

            var lease = new FileStream(
                snapshotFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.SequentialScan
            );
            return (snapshotFile, lease);
        }
        catch
        {
            File.Delete(snapshotFile);
            throw;
        }
    }

    internal static string CreateOwnedSiblingPath(string file, string purpose)
    {
        var normalizedFile = Path.GetFullPath(file);
        var directory = Path.GetDirectoryName(normalizedFile)!;
        var fileName = Path.GetFileName(normalizedFile);
        return Path.Combine(directory, $".{fileName}-{Guid.NewGuid():N}.{purpose}.tmp");
    }

    internal static string CreateLegacyClaimPath(string legacyFile)
    {
        return CreateOwnedSiblingPath(legacyFile, "legacy-claim");
    }

    internal static (FileStream Stream, UnixFileMode? UnixMode) OpenProtectedReadLease(
        string file,
        bool allowDelete
    )
    {
        var stream = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | (allowDelete ? FileShare.Delete : 0),
            CopyBufferSize,
            FileOptions.SequentialScan
        );
        UnixFileMode? unixMode = null;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                unixMode = File.GetUnixFileMode(file);
                File.SetUnixFileMode(
                    file,
                    unixMode.Value &
                    ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)
                );
            }

            return (stream, unixMode);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    internal static void ReleaseProtectedReadLease(
        string file,
        (FileStream Stream, UnixFileMode? UnixMode) lease
    )
    {
        lease.Stream.Dispose();

        if (!OperatingSystem.IsWindows() && lease.UnixMode is not null && File.Exists(file))
        {
            File.SetUnixFileMode(file, lease.UnixMode.Value);
        }
    }

    internal static bool FilesEqual(string leftFile, string rightFile)
    {
        using var left = new FileStream(
            leftFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            CopyBufferSize,
            FileOptions.SequentialScan
        );
        using var right = new FileStream(
            rightFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            CopyBufferSize,
            FileOptions.SequentialScan
        );

        return StreamsEqual(left, right);
    }

    internal static bool StreamsEqual(FileStream left, FileStream right)
    {
        left.Position = 0;
        right.Position = 0;

        if (left.Length != right.Length)
        {
            return false;
        }

        var leftBuffer = new byte[CopyBufferSize];
        var rightBuffer = new byte[CopyBufferSize];

        while (true)
        {
            var leftRead = left.Read(leftBuffer, 0, leftBuffer.Length);
            var rightRead = right.Read(rightBuffer, 0, rightBuffer.Length);

            if (leftRead != rightRead)
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }

            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
            {
                return false;
            }
        }
    }

    private static (int StandardCount, int CustomCount, int FileCount) BuildMigrationDirectory(
        Assembly assembly,
        string embeddedDirectory,
        string embeddedNamespace,
        string legacyFile,
        string legacySnapshot,
        string normalizedTarget,
        string temporaryDirectory
    )
    {
        var temporaryPrefix = temporaryDirectory + Path.DirectorySeparatorChar;

        try
        {
            Directory.CreateDirectory(temporaryDirectory);

            var resources = ResourceUtils.GetEmbeddedResourceNames(assembly, embeddedDirectory)
                .Where(resourceName => resourceName.StartsWith(embeddedNamespace + ".", StringComparison.Ordinal))
                .OrderBy(resourceName => resourceName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (resources.Length == 0)
            {
                throw new InvalidDataException(
                    $"No embedded item template resources were found under '{embeddedDirectory}'."
                );
            }

            var standardFiles = new List<(string RelativePath, ItemTemplate[] Templates)>();
            var defaultSources = new List<ItemTemplateSource>();
            var defaultById = new Dictionary<string, ItemTemplate>(StringComparer.OrdinalIgnoreCase);
            var defaultMembership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var observedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceName in resources)
            {
                var relativePath = ResourceUtils.ConvertResourceNameToPath(resourceName, embeddedNamespace);
                var destination = Path.GetFullPath(Path.Combine(temporaryDirectory, relativePath));

                if (string.Equals(destination, temporaryDirectory, StringComparison.Ordinal) ||
                    !destination.StartsWith(temporaryPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Embedded resource '{resourceName}' resolves outside item template root '{normalizedTarget}'."
                    );
                }

                if (!observedRelativePaths.Add(relativePath))
                {
                    throw new InvalidDataException(
                        $"Duplicate embedded item template path '{relativePath}'."
                    );
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(
                    destination,
                    ResourceUtils.GetEmbeddedResourceByteArray(assembly, resourceName).ToArray()
                );

                var templates = ItemTemplateYamlDeserializer.DeserializeFromFile(destination, relativePath);
                standardFiles.Add((relativePath, templates));

                foreach (var template in templates)
                {
                    if (!defaultById.TryAdd(template.Id, template))
                    {
                        throw new InvalidDataException(
                            $"Duplicate embedded item template ID '{template.Id}' in '{relativePath}' " +
                            $"and '{defaultMembership[template.Id]}'."
                        );
                    }

                    defaultMembership.Add(template.Id, relativePath);
                    defaultSources.Add(new ItemTemplateSource(relativePath, template));
                }
            }

            ItemTemplateValidator.Validate(defaultSources);

            var legacyRelativePath = Path.GetFileName(legacyFile);
            var legacyTemplates = ItemTemplateYamlDeserializer.DeserializeFromFile(legacySnapshot, legacyRelativePath);
            var legacySources = legacyTemplates
                .Select(template => new ItemTemplateSource(legacyRelativePath, template))
                .ToArray();
            ItemTemplateValidator.Validate(legacySources);

            var legacyById = legacyTemplates.ToDictionary(template => template.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var (relativePath, templates) in standardFiles)
            {
                var merged = templates
                    .Select(template => legacyById.GetValueOrDefault(template.Id) ?? template)
                    .ToArray();
                ItemTemplateYamlSerializer.SerializeToFile(
                    Path.Combine(temporaryDirectory, relativePath),
                    merged
                );
            }

            var customTemplates = legacyTemplates
                .Where(template => !defaultById.ContainsKey(template.Id))
                .OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(template => template.Id, StringComparer.Ordinal)
                .ToArray();

            if (customTemplates.Length > 0)
            {
                const string customRelativePath = "custom.yaml";

                if (observedRelativePaths.Contains(customRelativePath))
                {
                    throw new InvalidDataException(
                        $"Embedded item template path '{customRelativePath}' conflicts with migration output."
                    );
                }

                ItemTemplateYamlSerializer.SerializeToFile(
                    Path.Combine(temporaryDirectory, customRelativePath),
                    customTemplates
                );
            }

            var generatedFiles = Directory.GetFiles(temporaryDirectory, "*.yaml", SearchOption.AllDirectories)
                .OrderBy(file => Path.GetRelativePath(temporaryDirectory, file), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var generatedSources = new List<ItemTemplateSource>();

            foreach (var generatedFile in generatedFiles)
            {
                var relativePath = Path.GetRelativePath(temporaryDirectory, generatedFile);
                var templates = ItemTemplateYamlDeserializer.DeserializeFromFile(generatedFile, relativePath);
                generatedSources.AddRange(
                    templates.Select(template => new ItemTemplateSource(relativePath, template))
                );
            }

            ItemTemplateValidator.Validate(generatedSources);

            return (defaultSources.Count, customTemplates.Length, generatedFiles.Length);
        }
        catch
        {
            DeleteDirectoryIfExists(temporaryDirectory);
            throw;
        }
    }

    private static (FileStream Stream, UnixFileMode? UnixMode) PublishBackupAtomic(
        string legacySnapshot,
        FileStream snapshotLease,
        string backupFile
    )
    {
        if (File.Exists(backupFile))
        {
            return AcquireVerifiedBackupLease(backupFile, snapshotLease);
        }

        var temporaryBackup = CreateOwnedSiblingPath(backupFile, "publish");
        FileStream? temporaryBackupLease = null;

        try
        {
            temporaryBackupLease = CopyToDurableFile(
                legacySnapshot,
                temporaryBackup,
                FileShare.Read | FileShare.Delete
            );

            if (!FilesEqual(temporaryBackup, legacySnapshot))
            {
                throw new InvalidDataException("Temporary item template migration backup verification failed.");
            }

            Checkpoint("BeforeBackupPublish", backupFile);

            try
            {
                File.Move(temporaryBackup, backupFile, overwrite: false);
            }
            catch (IOException) when (File.Exists(backupFile))
            {
            }

            temporaryBackupLease.Dispose();
            temporaryBackupLease = null;
            return AcquireVerifiedBackupLease(backupFile, snapshotLease);
        }
        finally
        {
            temporaryBackupLease?.Dispose();
            File.Delete(temporaryBackup);
        }
    }

    private static (FileStream Stream, UnixFileMode? UnixMode) AcquireVerifiedBackupLease(
        string backupFile,
        FileStream snapshotLease
    )
    {
        var backupLease = OpenProtectedReadLease(backupFile, allowDelete: false);

        if (StreamsEqual(backupLease.Stream, snapshotLease))
        {
            return backupLease;
        }

        ReleaseProtectedReadLease(backupFile, backupLease);
        throw DifferentBackup();
    }

    private static void DeleteLegacyIfSnapshotMatches(
        string legacyFile,
        string legacySnapshot,
        FileStream snapshotLease
    )
    {
        if (!File.Exists(legacyFile) || !FilesEqual(legacyFile, legacySnapshot))
        {
            return;
        }

        var claimedLegacy = CreateLegacyClaimPath(legacyFile);
        var claimed = false;
        (FileStream Stream, UnixFileMode? UnixMode)? claimedLease = null;

        try
        {
            Checkpoint("BeforeLegacyClaim", legacyFile);
            File.Move(legacyFile, claimedLegacy, overwrite: false);
            claimed = true;

            claimedLease = OpenProtectedReadLease(claimedLegacy, allowDelete: true);

            if (!StreamsEqual(claimedLease.Value.Stream, snapshotLease))
            {
                ReleaseProtectedReadLease(claimedLegacy, claimedLease.Value);
                claimedLease = null;
                RestoreClaim(claimedLegacy, legacyFile);
                claimed = false;
                return;
            }

            Checkpoint("BeforeLegacyDelete", claimedLegacy);
            File.Delete(claimedLegacy);
            ReleaseProtectedReadLease(claimedLegacy, claimedLease.Value);
            claimedLease = null;
            claimed = false;
        }
        catch
        {
            if (claimedLease is not null)
            {
                ReleaseProtectedReadLease(claimedLegacy, claimedLease.Value);
            }

            if (claimed && File.Exists(claimedLegacy) && !File.Exists(legacyFile))
            {
                File.Move(claimedLegacy, legacyFile, overwrite: false);
            }

            throw;
        }
    }

    private static void RestoreClaim(string claimedFile, string originalFile)
    {
        if (File.Exists(originalFile))
        {
            throw new IOException(
                $"Changed legacy item template data was retained at '{claimedFile}' because '{originalFile}' now exists."
            );
        }

        File.Move(claimedFile, originalFile, overwrite: false);
    }

    private static FileStream CopyToDurableFile(
        string sourceFile,
        string destinationFile,
        FileShare destinationShare
    )
    {
        using var source = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.SequentialScan
        );
        var destination = new FileStream(
            destinationFile,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            destinationShare,
            CopyBufferSize,
            FileOptions.WriteThrough
        );

        try
        {
            source.CopyTo(destination, CopyBufferSize);
            destination.Flush(flushToDisk: true);
            destination.Position = 0;
            return destination;
        }
        catch
        {
            destination.Dispose();
            File.Delete(destinationFile);
            throw;
        }
    }

    private static InvalidDataException DifferentBackup()
    {
        return new InvalidDataException(
            "Existing item template migration backup differs from legacy source."
        );
    }

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static void Checkpoint(string checkpoint, string path)
    {
        _checkpointForTests?.Invoke(checkpoint, path);
    }
}
