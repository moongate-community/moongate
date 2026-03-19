using System.Collections.Concurrent;
using MessagePack;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Persists full world snapshots using MessagePack binary serialization.
/// </summary>
public sealed class MessagePackSnapshotService : ISnapshotService, IDisposable
{
    private static readonly ConcurrentDictionary<string, byte> LockedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly ILogger _logger = Log.ForContext<MessagePackSnapshotService>();
    private readonly string _snapshotFilePath;
    private readonly FileStream _snapshotStream;
    private readonly bool _fileLockEnabled;

    public MessagePackSnapshotService(string snapshotFilePath, bool enableFileLock = true)
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

    public void Dispose()
    {
        _snapshotStream.Dispose();
        _ioLock.Dispose();

        if (_fileLockEnabled)
        {
            LockedPaths.TryRemove(_snapshotFilePath, out _);
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
            var payloadLength = checked((int)_snapshotStream.Length);
            var payload = new byte[payloadLength];
            var totalRead = 0;

            while (totalRead < payloadLength)
            {
                var read = await _snapshotStream.ReadAsync(
                               payload.AsMemory(totalRead, payloadLength - totalRead),
                               cancellationToken
                           );

                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead != payloadLength)
            {
                _logger.Warning(
                    "Snapshot load short-read Path={SnapshotPath} Expected={ExpectedBytes} Read={ReadBytes}",
                    _snapshotFilePath,
                    payloadLength,
                    totalRead
                );

                return null;
            }

            var snapshot = MessagePackSerializer.Deserialize<WorldSnapshot>(payload, cancellationToken: cancellationToken);
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
            "Snapshot save requested Path={SnapshotPath} BucketCount={BucketCount}",
            _snapshotFilePath,
            snapshot.EntityBuckets.Length
        );
        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            var payload = MessagePackSerializer.Serialize(snapshot, cancellationToken: cancellationToken);
            _snapshotStream.SetLength(0);
            _snapshotStream.Position = 0;

            await _snapshotStream.WriteAsync(payload, cancellationToken);
            await _snapshotStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }

        _logger.Verbose("Snapshot save completed Path={SnapshotPath}", _snapshotFilePath);
    }
}
