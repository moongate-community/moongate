using System.Text.Json;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Services;

/// <summary>Restores verified persistence backup generations into a new directory.</summary>
public static class MoongatePersistenceBackup
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>The manifest present in each completed backup generation.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>Verifies a backup and publishes its contents at a previously nonexistent destination.</summary>
    public static async Task RestoreAsync(
        string backupDirectory,
        string destinationDirectory,
        CancellationToken cancellationToken = default
    )
    {
        var destination = PersistenceBackupFiles.ValidateDestination(backupDirectory, destinationDirectory);
        var source = PersistenceBackupFiles.ResolveDirectoryPath(backupDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = await ReadManifestAsync(source, cancellationToken).ConfigureAwait(false);
        ValidateManifest(source, manifest);
        await PersistenceBackupFiles.PublishDirectoryAsync(destination, async (staging, token) =>
        {
            foreach (var collection in manifest.Collections)
            {
                await CopyAndVerifyAsync(source, staging, collection.Snapshot, token).ConfigureAwait(false);
                await CopyAndVerifyAsync(source, staging, collection.Journal, token).ConfigureAwait(false);
                ValidateCheckpoint(staging, collection, token);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task WriteAsync(
        string source,
        string staging,
        string[] collectionNames,
        CancellationToken cancellationToken
    )
    {
        var collections = new List<PersistenceBackupCollection>(collectionNames.Length);
        foreach (var name in collectionNames)
        {
            var snapshotName = name + ".snapshot.bin";
            var journalName = name + ".journal.bin";
            var snapshot = await PersistenceBackupFiles.CopyAsync(Path.Combine(source, snapshotName),
                Path.Combine(staging, snapshotName), allowSourceWriter: true, cancellationToken).ConfigureAwait(false);
            var journal = await PersistenceBackupFiles.CopyAsync(Path.Combine(source, journalName),
                Path.Combine(staging, journalName), allowSourceWriter: true, cancellationToken).ConfigureAwait(false);
            collections.Add(new PersistenceBackupCollection { Name = name, Snapshot = snapshot, Journal = journal });
        }
        var manifest = new PersistenceBackupManifest { FormatVersion = 1, Collections = [.. collections] };
        await using var output = new FileStream(Path.Combine(staging, ManifestFileName), FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(output, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        output.Flush(flushToDisk: true);
    }

    private static async Task<PersistenceBackupManifest> ReadManifestAsync(string directory, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(directory, ManifestFileName);
            if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            {
                throw new InvalidDataException("The backup manifest must be a regular file.");
            }
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);

            return await JsonSerializer.DeserializeAsync<PersistenceBackupManifest>(input, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("The backup manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The backup manifest is invalid.", exception);
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidDataException("The backup manifest is missing.", exception);
        }
    }

    private static void ValidateManifest(string directory, PersistenceBackupManifest manifest)
    {
        if (manifest.FormatVersion != 1 || manifest.Collections is null)
        {
            throw new InvalidDataException("Unsupported or incomplete backup manifest.");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var expectedFiles = new HashSet<string>(StringComparer.Ordinal) { ManifestFileName };
        foreach (var collection in manifest.Collections)
        {
            if (collection is null)
            {
                throw new InvalidDataException("The backup contains a null collection.");
            }
            try
            {
                PersistencePaths.ValidateCollectionName(collection.Name);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("The backup contains an invalid collection name.", exception);
            }
            if (!names.Add(collection.Name))
            {
                throw new InvalidDataException("The backup contains a duplicate collection.");
            }
            ValidateFile(collection.Snapshot, collection.Name + ".snapshot.bin", expectedFiles);
            ValidateFile(collection.Journal, collection.Name + ".journal.bin", expectedFiles);
        }
        var actualFiles = Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        if (!expectedFiles.SetEquals(actualFiles))
        {
            throw new InvalidDataException("The backup files do not match the manifest.");
        }
    }

    private static void ValidateFile(PersistenceBackupFile? file, string expectedName, HashSet<string> expectedFiles)
    {
        if (file is null || file.FileName != expectedName || !expectedFiles.Add(file.FileName) ||
            file.Length < BinaryPersistenceFormat.HeaderSize || file.Sha256 is null || file.Sha256.Length != 64 ||
            file.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("The backup contains invalid or duplicate file metadata.");
        }
    }

    private static async Task CopyAndVerifyAsync(
        string source,
        string staging,
        PersistenceBackupFile expected,
        CancellationToken cancellationToken
    )
    {
        var actual = await PersistenceBackupFiles.CopyAsync(Path.Combine(source, expected.FileName),
            Path.Combine(staging, expected.FileName), allowSourceWriter: false, cancellationToken).ConfigureAwait(false);
        if (actual.Length != expected.Length || !string.Equals(actual.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Backup file '{expected.FileName}' does not match its declared length or SHA-256.");
        }
    }

    private static void ValidateCheckpoint(
        string directory,
        PersistenceBackupCollection collection,
        CancellationToken cancellationToken
    )
    {
        var snapshotPath = Path.Combine(directory, collection.Snapshot.FileName);
        var journalPath = Path.Combine(directory, collection.Journal.FileName);
        using var snapshot = File.OpenRead(snapshotPath);
        using var journal = File.OpenRead(journalPath);
        var snapshotHeader = BinaryPersistenceFormat.ReadHeader(snapshot, snapshotPath, collection.Name, PersistenceFileKind.Snapshot);
        var journalHeader = BinaryPersistenceFormat.ReadHeader(journal, journalPath, collection.Name, PersistenceFileKind.Journal);
        if (journal.Length != BinaryPersistenceFormat.HeaderSize || journalHeader.Sequence != snapshotHeader.Sequence ||
            snapshotHeader.Count > (ulong)((snapshot.Length - snapshot.Position) / (BinaryPersistenceFormat.RecordHeaderSize + 1)))
        {
            throw new InvalidDataException("The backup files are not a completed checkpoint pair.");
        }
        var identities = new HashSet<Moongate.Core.Primitives.Serial>();
        for (ulong index = 0; index < snapshotHeader.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = BinaryPersistenceFormat.ReadRecord(snapshot, snapshotPath, Array.MaxLength);
            if (record is null || record.Operation != PersistenceOperation.Upsert ||
                record.Sequence != snapshotHeader.Sequence || !identities.Add(record.Id))
            {
                throw new InvalidDataException("The backup contains an invalid snapshot record.");
            }
        }
        if (snapshot.Position != snapshot.Length)
        {
            throw new InvalidDataException("The backup snapshot contains trailing bytes.");
        }
    }
}
