using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Types;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

public sealed class BulletinBoardMessageRepository : IBulletinBoardMessageRepository
{
    private readonly IJournalService _journalService;
    private readonly PersistenceStateStore _stateStore;
    private readonly ILogger _logger = Log.ForContext<BulletinBoardMessageRepository>();

    internal BulletinBoardMessageRepository(PersistenceStateStore stateStore, IJournalService journalService)
    {
        _stateStore = stateStore;
        _journalService = journalService;
    }

    public ValueTask<IReadOnlyCollection<BulletinBoardMessageEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult<IReadOnlyCollection<BulletinBoardMessageEntity>>(
                [.. _stateStore.BulletinBoardMessagesById.Values.Select(Clone)]
            );
        }
    }

    public ValueTask<BulletinBoardMessageEntity?> GetByIdAsync(Serial messageId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult(
                _stateStore.BulletinBoardMessagesById.TryGetValue(messageId, out var message)
                    ? Clone(message)
                    : null
            );
        }
    }

    public ValueTask<IReadOnlyList<BulletinBoardMessageEntity>> GetByBoardIdAsync(Serial boardId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult<IReadOnlyList<BulletinBoardMessageEntity>>(
                [
                    .. _stateStore.BulletinBoardMessagesById.Values
                                 .Where(message => message.BoardId == boardId)
                                 .OrderBy(message => message.PostedAtUtc)
                                 .Select(Clone)
                ]
            );
        }
    }

    public async ValueTask UpsertAsync(BulletinBoardMessageEntity message, CancellationToken cancellationToken = default)
    {
        JournalEntry entry;

        lock (_stateStore.SyncRoot)
        {
            var clone = Clone(message);
            _stateStore.BulletinBoardMessagesById[clone.MessageId] = clone;
            _stateStore.LastItemId = Math.Max(_stateStore.LastItemId, (uint)clone.MessageId);
            entry = CreateEntry(PersistenceOperationType.UpsertBulletinBoardMessage, JournalPayloadCodec.EncodeBulletinBoardMessage(clone));
        }

        await _journalService.AppendAsync(entry, cancellationToken);
        _logger.Verbose("Bulletin board message upsert completed for MessageId={MessageId}", message.MessageId);
    }

    public async ValueTask<bool> RemoveAsync(Serial messageId, CancellationToken cancellationToken = default)
    {
        var removed = false;
        JournalEntry? entry = null;

        lock (_stateStore.SyncRoot)
        {
            if (_stateStore.BulletinBoardMessagesById.Remove(messageId))
            {
                removed = true;
                entry = CreateEntry(PersistenceOperationType.RemoveBulletinBoardMessage, JournalPayloadCodec.EncodeSerial(messageId));
            }
        }

        if (removed && entry is not null)
        {
            await _journalService.AppendAsync(entry, cancellationToken);
        }

        return removed;
    }

    private static BulletinBoardMessageEntity Clone(BulletinBoardMessageEntity message)
        => SnapshotMapper.ToBulletinBoardMessageEntity(SnapshotMapper.ToBulletinBoardMessageSnapshot(message));

    private JournalEntry CreateEntry(PersistenceOperationType operationType, byte[] payload)
        => new()
        {
            SequenceId = ++_stateStore.LastSequenceId,
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            OperationType = operationType,
            Payload = payload
        };
}
