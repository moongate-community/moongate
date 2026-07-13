using System.Reflection;
using Moongate.Server.Data.Internal;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Items;
using Serilog;
using SquidStd.Core.Utils;

namespace Moongate.Server.Internal;

public static class ItemTemplateDirectoryMigrator
{
    private const int CopyBufferSize = 81920;

    private static readonly ILogger Logger = Log.ForContext(typeof(ItemTemplateDirectoryMigrator));
    private static Action<string>? _ioFailureForTests = null;

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
        var temporaryPrefix = temporaryDirectory + Path.DirectorySeparatorChar;
        var legacySnapshot = CreateOwnedSiblingPath(legacyFile, "snapshot");

        if (Directory.Exists(normalizedTarget))
        {
            throw new IOException($"Item template target directory '{normalizedTarget}' already exists.");
        }

        Directory.CreateDirectory(parentDirectory);

        try
        {
            CopyToDurableFile(legacyFile, legacySnapshot);
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
            var legacyTemplates = ItemTemplateYamlDeserializer.DeserializeFromFile(
                legacySnapshot,
                legacyRelativePath
            );
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

            PublishBackup(legacySnapshot, backupFile);

            Directory.Move(temporaryDirectory, normalizedTarget);
            TryDeleteLegacyAfterCommit(legacyFile, legacySnapshot);

            return new ItemTemplateMigrationResult(
                defaultSources.Count,
                customTemplates.Length,
                generatedFiles.Length,
                backupFile
            );
        }
        catch
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }

            throw;
        }
        finally
        {
            TryDeleteOwnedTemporaryFile(legacySnapshot);
        }
    }

    internal static bool FilesEqual(string leftFile, string rightFile)
    {
        using var left = new FileStream(
            leftFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.SequentialScan
        );
        using var right = new FileStream(
            rightFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.SequentialScan
        );

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

    private static void PublishBackup(string legacySnapshot, string backupFile)
    {
        if (File.Exists(backupFile))
        {
            if (!FilesEqual(backupFile, legacySnapshot))
            {
                throw new InvalidDataException(
                    "Existing item template migration backup differs from legacy source."
                );
            }

            return;
        }

        var temporaryBackup = CreateOwnedSiblingPath(backupFile, "publish");

        try
        {
            CopyToDurableFile(legacySnapshot, temporaryBackup);

            if (!FilesEqual(temporaryBackup, legacySnapshot))
            {
                throw new InvalidDataException("Temporary item template migration backup verification failed.");
            }

            InjectIoFailure("backup-publish");
            File.Move(temporaryBackup, backupFile, overwrite: false);
        }
        finally
        {
            TryDeleteOwnedTemporaryFile(temporaryBackup);
        }
    }

    private static void TryDeleteLegacyAfterCommit(string legacyFile, string legacySnapshot)
    {
        try
        {
            InjectIoFailure("legacy-compare");

            if (!FilesEqual(legacyFile, legacySnapshot))
            {
                Logger.Warning(
                    "Leaving legacy item template source {LegacyPath} because it differs from the migration snapshot",
                    legacyFile
                );
                return;
            }

            InjectIoFailure("legacy-delete");
            File.Delete(legacyFile);
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            Logger.Warning(
                exception,
                "Unable to remove legacy item template source {LegacyPath} after target commit; leaving it in place",
                legacyFile
            );
        }
    }

    private static void CopyToDurableFile(string sourceFile, string destinationFile)
    {
        using var source = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.SequentialScan
        );
        using var destination = new FileStream(
            destinationFile,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.WriteThrough
        );
        source.CopyTo(destination, CopyBufferSize);
        destination.Flush(flushToDisk: true);
    }

    private static string CreateOwnedSiblingPath(string file, string purpose)
    {
        var normalizedFile = Path.GetFullPath(file);
        var directory = Path.GetDirectoryName(normalizedFile)!;
        var fileName = Path.GetFileName(normalizedFile);
        return Path.Combine(directory, $".{fileName}-{Guid.NewGuid():N}.{purpose}.tmp");
    }

    private static void TryDeleteOwnedTemporaryFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (IsFileIoFailure(exception))
        {
            Logger.Warning(exception, "Unable to remove owned item template temporary file {Path}", file);
        }
    }

    private static bool IsFileIoFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    private static void InjectIoFailure(string phase)
    {
        _ioFailureForTests?.Invoke(phase);
    }
}
