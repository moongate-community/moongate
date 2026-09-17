using System.Security.Cryptography;
using Moongate.Persistence.Data.Internal;

namespace Moongate.Persistence.Internal;

internal static class PersistenceBackupFiles
{
    public static string ValidateDestination(string sourceDirectory, string destinationDirectory)
    {
        var source = ResolveDirectoryPath(sourceDirectory);
        var destination = ResolveDirectoryPath(destinationDirectory);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(source, destination, comparison) ||
            source.StartsWith(AppendSeparator(destination), comparison) ||
            destination.StartsWith(AppendSeparator(source), comparison))
        {
            throw new ArgumentException("Backup source and destination directories must not overlap.", nameof(destinationDirectory));
        }

        EnsureDestinationAbsent(destinationDirectory);
        EnsureDestinationAbsent(destination);

        return destination;
    }

    public static string ResolveDirectoryPath(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullPath = Path.GetFullPath(directory);
        var current = Path.GetPathRoot(fullPath)!;
        foreach (var part in fullPath[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            var info = new DirectoryInfo(current);
            if (info.LinkTarget is not null)
            {
                current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ??
                    throw new IOException($"Cannot resolve directory link '{current}'.");
            }
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    public static async Task PublishDirectoryAsync(
        string destination,
        Func<string, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDestinationAbsent(destination);
        var parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        EnsureDestinationAbsent(staging);
        Directory.CreateDirectory(staging);
        try
        {
            await writeAsync(staging, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDestinationAbsent(destination);
            Directory.Move(staging, destination);
        }
        finally
        {
            // This path belongs only to this operation. Never clean up the requested destination.
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    public static async Task<PersistenceBackupFile> CopyAsync(
        string source,
        string destination,
        bool allowSourceWriter,
        CancellationToken cancellationToken
    )
    {
        if ((File.GetAttributes(source) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
        {
            throw new InvalidDataException($"Backup input '{source}' must be a regular file.");
        }

        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read,
            allowSourceWriter ? FileShare.ReadWrite : FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[131072];
        long length = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            length = checked(length + read);
        }
        cancellationToken.ThrowIfCancellationRequested();
        output.Flush(flushToDisk: true);

        return new PersistenceBackupFile
        {
            FileName = Path.GetFileName(destination),
            Length = length,
            Sha256 = Convert.ToHexString(hash.GetHashAndReset())
        };
    }

    private static string AppendSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }

    private static void EnsureDestinationAbsent(string destination)
    {
        if (Path.Exists(destination) || new FileInfo(destination).LinkTarget is not null)
        {
            throw new IOException($"Destination '{destination}' already exists.");
        }
    }
}
