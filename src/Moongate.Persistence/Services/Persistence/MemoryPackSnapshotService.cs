using MemoryPack;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Serilog;
using System.Collections.Concurrent;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Persists full world snapshots using MemoryPack binary serialization.
/// </summary>
public sealed class MemoryPackSnapshotService : ISnapshotService, IDisposable
{
    private static readonly ConcurrentDictionary<string, byte> LockedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly ILogger _logger = Log.ForContext<MemoryPackSnapshotService>();
    private readonly string _snapshotFilePath;
    private readonly FileStream _snapshotStream;
    private readonly bool _fileLockEnabled;

    public MemoryPackSnapshotService(string snapshotFilePath, bool enableFileLock = true)
    {
        _snapshotFilePath = Path.GetFullPath(snapshotFilePath);
        _fileLockEnabled = enableFileLock;
        var directoryPath = Path.GetDirectoryName(_snapshotFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (_fileLockEnabled && !LockedPaths.TryAdd(_snapshotFilePath, 0))
        {
            throw new IOException($"Snapshot file is already locked by this process: {_snapshotFilePath}");
        }

        try
        {
            _snapshotStream = new(
                _snapshotFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                enableFileLock ? FileShare.Read : FileShare.ReadWrite
            );
        }
        catch
        {
            if (_fileLockEnabled)
            {
                LockedPaths.TryRemove(_snapshotFilePath, out _);
            }

            throw;
        }
    }

    public async ValueTask<WorldSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Snapshot load requested Path={SnapshotPath}", _snapshotFilePath);

        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            if (_snapshotStream.Length == 0)
            {
                _logger.Verbose("Snapshot file is empty Path={SnapshotPath}", _snapshotFilePath);

                return null;
            }

            _snapshotStream.Position = 0;

            var snapshot = await MemoryPackSerializer.DeserializeAsync<WorldSnapshot>(
                               _snapshotStream,
                               cancellationToken: cancellationToken
                           );
            _logger.Verbose(
                "Snapshot load completed Path={SnapshotPath} Found={Found}",
                _snapshotFilePath,
                snapshot is not null
            );

            return snapshot;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async ValueTask SaveAsync(WorldSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _logger.Verbose(
            "Snapshot save requested Path={SnapshotPath} Accounts={AccountCount} Mobiles={MobileCount} Items={ItemCount}",
            _snapshotFilePath,
            snapshot.Accounts.Length,
            snapshot.Mobiles.Length,
            snapshot.Items.Length
        );
        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            _snapshotStream.SetLength(0);
            _snapshotStream.Position = 0;

            await MemoryPackSerializer.SerializeAsync(
                _snapshotStream,
                snapshot,
                cancellationToken: cancellationToken
            );
            await _snapshotStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }

        _logger.Verbose("Snapshot save completed Path={SnapshotPath}", _snapshotFilePath);
    }

    public void Dispose()
    {
        _snapshotStream.Dispose();
        _ioLock.Dispose();

        if (_fileLockEnabled)
        {
            LockedPaths.TryRemove(_snapshotFilePath, out _);
        }
    }
}
