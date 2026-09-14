using Serilog;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Internal;

internal sealed class BinaryCollectionFiles : IDisposable
{
    private readonly ILogger _logger = Log.ForContext<BinaryCollectionFiles>();
    private readonly PersistencePaths _paths;
    private readonly IPersistenceFileSystem _fileSystem;
    private Stream? _lock;
    private Stream? _journal;

    public long JournalLength => Journal.Length;

    private Stream Journal => _journal ?? throw new InvalidOperationException("Journal is not open.");

    public BinaryCollectionFiles(PersistencePaths paths, IPersistenceFileSystem fileSystem)
    {
        _paths = paths;
        _fileSystem = fileSystem;
    }

    public ulong Initialize(Dictionary<Serial, byte[]> entries, int maxPayloadBytes, CancellationToken cancellationToken)
    {
        _fileSystem.CreateDirectory(_paths.DirectoryPath);
        _lock = _fileSystem.Open(_paths.Lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var snapshotExists = _fileSystem.Exists(_paths.Snapshot);
        var journalExists = _fileSystem.Exists(_paths.Journal);
        if (snapshotExists != journalExists)
        {
            throw new InvalidDataException($"{_paths.DirectoryPath}: Both {_paths.CollectionName} snapshot and journal must exist together.");
        }

        if (!snapshotExists)
        {
            PublishSnapshot(0, entries);
            PublishJournal(0);
        }
        ulong snapshotSequence;
        using (var snapshot = _fileSystem.Open(_paths.Snapshot, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var header = BinaryPersistenceFormat.ReadHeader(snapshot, _paths.Snapshot, _paths.CollectionName, PersistenceFileKind.Snapshot);
            snapshotSequence = header.Sequence;
            // Each upsert needs at least its fixed header and one payload byte.
            if (header.Count > (ulong)((snapshot.Length - snapshot.Position) / (BinaryPersistenceFormat.RecordHeaderSize + 1)))
            {
                throw new InvalidDataException($"{_paths.Snapshot}: Snapshot count exceeds available records.");
            }

            for (ulong i = 0; i < header.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = BinaryPersistenceFormat.ReadRecord(snapshot, _paths.Snapshot, maxPayloadBytes);
                if (record is null || record.Operation != PersistenceOperation.Upsert || record.Sequence != snapshotSequence ||
                    !entries.TryAdd(record.Id, record.Payload))
                {
                    throw new InvalidDataException($"{_paths.Snapshot}: Invalid, incomplete or duplicate snapshot entry.");
                }
            }
            if (snapshot.Position != snapshot.Length)
            {
                throw new InvalidDataException($"{_paths.Snapshot}: Unexpected snapshot trailing bytes.");
            }
        }
        _journal = _fileSystem.Open(_paths.Journal, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        var journalHeader = BinaryPersistenceFormat.ReadHeader(Journal, _paths.Journal, _paths.CollectionName, PersistenceFileKind.Journal);
        if (journalHeader.Sequence > snapshotSequence)
        {
            throw new InvalidDataException($"{_paths.Journal}: Journal base exceeds snapshot sequence.");
        }
        var sequence = journalHeader.Sequence;
        var completeLength = Journal.Position;
        while (Journal.Position < Journal.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = BinaryPersistenceFormat.ReadRecord(Journal, _paths.Journal, maxPayloadBytes);
            if (record is null)
            {
                // Validate the complete prefix before repairing any bytes.
                if (sequence < snapshotSequence)
                {
                    throw new InvalidDataException($"{_paths.Journal}: Journal does not reach the snapshot sequence.");
                }
                _logger.Warning("Discarding incomplete journal tail at {Path}, offset {Offset}", _paths.Journal, completeLength);
                Journal.SetLength(completeLength);
                _fileSystem.FlushToDisk(Journal);
                break;
            }
            if (sequence == ulong.MaxValue || record.Sequence != sequence + 1)
            {
                throw new InvalidDataException($"{_paths.Journal}: Noncontiguous or overflowing journal sequence.");
            }
            sequence = record.Sequence;
            if (sequence > snapshotSequence)
            {
                if (record.Operation == PersistenceOperation.Upsert)
                {
                    entries[record.Id] = record.Payload;
                }
                else
                {
                    entries.Remove(record.Id);
                }
            }
            completeLength = Journal.Position;
        }
        if (sequence < snapshotSequence)
        {
            throw new InvalidDataException($"{_paths.Journal}: Journal does not reach the snapshot sequence.");
        }
        Journal.Position = Journal.Length;
        return sequence;
    }

    public void Append(PersistenceOperation operation, ulong sequence, Serial id, byte[] payload)
    {
        BinaryPersistenceFormat.WriteRecord(Journal, operation, sequence, id, payload);
        _fileSystem.FlushToDisk(Journal);
    }

    public void Checkpoint(ulong sequence, Dictionary<Serial, byte[]> entries)
    {
        PublishSnapshot(sequence, entries);
        PublishJournal(sequence);
        _journal = _fileSystem.Open(_paths.Journal, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        Journal.Position = Journal.Length;
    }

    private void PublishSnapshot(ulong sequence, Dictionary<Serial, byte[]> entries)
    {
        var temporary = _paths.Snapshot + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = _fileSystem.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                BinaryPersistenceFormat.WriteHeader(stream, _paths.CollectionName, PersistenceFileKind.Snapshot, sequence, (ulong)entries.Count);
                foreach (var entry in entries)
                {
                    BinaryPersistenceFormat.WriteRecord(stream, PersistenceOperation.Upsert, sequence, entry.Key, entry.Value);
                }
                _fileSystem.FlushToDisk(stream);
            }
            _fileSystem.Move(temporary, _paths.Snapshot);
        }
        finally
        {
            _fileSystem.Delete(temporary);
        }
    }

    private void PublishJournal(ulong sequence)
    {
        var temporary = _paths.Journal + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = _fileSystem.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                BinaryPersistenceFormat.WriteHeader(stream, _paths.CollectionName, PersistenceFileKind.Journal, sequence, 0);
                _fileSystem.FlushToDisk(stream);
            }
            _journal?.Dispose();
            _journal = null;
            _fileSystem.Move(temporary, _paths.Journal);
        }
        finally
        {
            _fileSystem.Delete(temporary);
        }
    }

    public void Dispose()
    {
        try
        {
            _journal?.Dispose();
        }
        finally
        {
            _journal = null;
            _lock?.Dispose();
            _lock = null;
        }
    }
}
