using System.Buffers.Binary;
using System.Collections.Concurrent;
using MessagePack;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Utils;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Stores append-only journal entries in a binary file with checksum validation.
/// </summary>
public sealed class BinaryJournalService : IJournalService, IDisposable
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IoLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> LockedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger = Log.ForContext<BinaryJournalService>();
    private readonly string _journalFilePath;
    private readonly FileStream _journalStream;
    private readonly SemaphoreSlim _ioLock;
    private readonly bool _fileLockEnabled;

    public BinaryJournalService(string journalFilePath, bool enableFileLock = true)
    {
        _journalFilePath = Path.GetFullPath(journalFilePath);
        _ioLock = IoLocks.GetOrAdd(_journalFilePath, _ => new(1, 1));
        _fileLockEnabled = enableFileLock;
        var directoryPath = Path.GetDirectoryName(_journalFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (_fileLockEnabled && !LockedPaths.TryAdd(_journalFilePath, 0))
        {
            throw new IOException($"Journal file is already locked by this process: {_journalFilePath}");
        }

        try
        {
            _journalStream = new(
                _journalFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                enableFileLock ? FileShare.Read : FileShare.ReadWrite
            );
        }
        catch
        {
            if (_fileLockEnabled)
            {
                LockedPaths.TryRemove(_journalFilePath, out _);
            }

            throw;
        }
    }

    public async ValueTask AppendAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        _logger.Verbose(
            "Journal append requested Path={JournalPath} SequenceId={SequenceId} OperationType={OperationType}",
            _journalFilePath,
            entry.SequenceId,
            entry.OperationType
        );
        var payload = MessagePackSerializer.Serialize(entry, cancellationToken: cancellationToken);
        var checksum = ChecksumUtils.Compute(payload);

        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);

        var checksumBuffer = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(checksumBuffer, checksum);

        var directoryPath = Path.GetDirectoryName(_journalFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            _journalStream.Position = _journalStream.Length;
            await _journalStream.WriteAsync(lengthBuffer, cancellationToken);
            await _journalStream.WriteAsync(payload, cancellationToken);
            await _journalStream.WriteAsync(checksumBuffer, cancellationToken);
            await _journalStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }

        _logger.Verbose(
            "Journal append completed Path={JournalPath} SequenceId={SequenceId}",
            _journalFilePath,
            entry.SequenceId
        );
    }

    public async ValueTask AppendBatchAsync(IReadOnlyList<JournalEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var directoryPath = Path.GetDirectoryName(_journalFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            _journalStream.Position = _journalStream.Length;

            var lengthBuffer = new byte[4];
            var checksumBuffer = new byte[4];

            foreach (var entry in entries)
            {
                var payload = MessagePackSerializer.Serialize(entry, cancellationToken: cancellationToken);
                var checksum = ChecksumUtils.Compute(payload);

                BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(checksumBuffer, checksum);

                await _journalStream.WriteAsync(lengthBuffer, cancellationToken);
                await _journalStream.WriteAsync(payload, cancellationToken);
                await _journalStream.WriteAsync(checksumBuffer, cancellationToken);
            }

            await _journalStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }

        _logger.Verbose(
            "Journal batch append completed Path={JournalPath} Count={Count}",
            _journalFilePath,
            entries.Count
        );
    }

    public void Dispose()
    {
        _journalStream.Dispose();

        if (_fileLockEnabled)
        {
            LockedPaths.TryRemove(_journalFilePath, out _);
        }
    }

    public async ValueTask<IReadOnlyCollection<JournalEntry>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Journal read-all requested Path={JournalPath}", _journalFilePath);

        var entries = new List<JournalEntry>();

        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            if (_journalStream.Length == 0)
            {
                _logger.Verbose("Journal file is empty Path={JournalPath}", _journalFilePath);

                return [];
            }

            _journalStream.Position = 0;
            var lengthBuffer = new byte[4];
            var checksumBuffer = new byte[4];

            while (true)
            {
                var lengthBytesRead = await _journalStream.ReadAsync(lengthBuffer, cancellationToken);

                if (lengthBytesRead == 0)
                {
                    break;
                }

                if (lengthBytesRead != 4)
                {
                    _logger.Warning("Journal truncated at record-length read Path={JournalPath}", _journalFilePath);

                    break;
                }

                var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

                if (payloadLength <= 0 || payloadLength > 16 * 1024 * 1024)
                {
                    _logger.Warning(
                        "Journal invalid payload length Path={JournalPath} PayloadLength={PayloadLength}",
                        _journalFilePath,
                        payloadLength
                    );

                    break;
                }

                var payload = new byte[payloadLength];
                var payloadBytesRead = await _journalStream.ReadAsync(payload, cancellationToken);

                if (payloadBytesRead != payloadLength)
                {
                    _logger.Warning(
                        "Journal truncated at payload read Path={JournalPath} PayloadLength={PayloadLength}",
                        _journalFilePath,
                        payloadLength
                    );

                    break;
                }

                var checksumBytesRead = await _journalStream.ReadAsync(checksumBuffer, cancellationToken);

                if (checksumBytesRead != 4)
                {
                    _logger.Warning("Journal truncated at checksum read Path={JournalPath}", _journalFilePath);

                    break;
                }

                var expectedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(checksumBuffer);
                var actualChecksum = ChecksumUtils.Compute(payload);

                if (expectedChecksum != actualChecksum)
                {
                    _logger.Warning("Journal checksum mismatch Path={JournalPath}", _journalFilePath);

                    break;
                }

                var entry = MessagePackSerializer.Deserialize<JournalEntry>(payload, cancellationToken: cancellationToken);

                if (entry is null)
                {
                    _logger.Warning("Journal entry deserialize failed Path={JournalPath}", _journalFilePath);

                    break;
                }

                entries.Add(entry);
            }
        }
        finally
        {
            _ioLock.Release();
        }

        _logger.Verbose("Journal read-all completed Path={JournalPath} Count={Count}", _journalFilePath, entries.Count);

        return entries;
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        _logger.Verbose("Journal reset requested Path={JournalPath}", _journalFilePath);
        await _ioLock.WaitAsync(cancellationToken);

        try
        {
            _journalStream.SetLength(0);
            _journalStream.Position = 0;
            await _journalStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _ioLock.Release();
        }

        _logger.Verbose("Journal reset completed Path={JournalPath}", _journalFilePath);
    }
}
