using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Types;

namespace Moongate.Persistence.Services.Persistence;

internal abstract class BaseRepository<TEntity, TKey> : IBaseRepository<TEntity, TKey>
    where TKey : notnull
{
    protected readonly IPersistenceEntityDescriptor<TEntity, TKey> _descriptor;
    protected readonly IJournalService _journalService;
    protected readonly PersistenceStateStore _stateStore;

    protected BaseRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<TEntity, TKey> descriptor
    )
    {
        _stateStore = stateStore;
        _journalService = journalService;
        _descriptor = descriptor;
    }

    public virtual ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult(GetBucket().Count);
        }
    }

    public virtual ValueTask<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult<IReadOnlyCollection<TEntity>>([.. GetBucket().Values.Select(CloneEntity)]);
        }
    }

    public virtual ValueTask<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        lock (_stateStore.SyncRoot)
        {
            return ValueTask.FromResult(GetBucket().TryGetValue(id, out var entity) ? CloneEntity(entity) : default);
        }
    }

    public virtual async ValueTask<bool> RemoveAsync(TKey id, CancellationToken cancellationToken = default)
    {
        JournalEntry? entry = null;
        var removed = false;

        lock (_stateStore.SyncRoot)
        {
            if (GetBucket().Remove(id, out var entity))
            {
                AfterRemoveLocked(id, entity);
                removed = true;
                entry = CreateRemoveEntry(id);
            }
        }

        if (removed && entry is not null)
        {
            await _journalService.AppendAsync(entry, cancellationToken);
        }

        return removed;
    }

    public virtual async ValueTask UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        JournalEntry entry;

        lock (_stateStore.SyncRoot)
        {
            var clone = PrepareEntityForStore(entity);
            var key = _descriptor.GetKey(clone);
            var bucket = GetBucket();
            bucket.TryGetValue(key, out var existing);
            BeforeUpsertLocked(clone, existing);
            bucket[key] = clone;
            entry = CreateUpsertEntry(clone);
        }

        await _journalService.AppendAsync(entry, cancellationToken);
    }

    protected virtual void AfterRemoveLocked(TKey key, TEntity entity) { }

    protected ValueTask AppendAsync(JournalEntry entry, CancellationToken cancellationToken = default)
        => _journalService.AppendAsync(entry, cancellationToken);

    protected ValueTask AppendBatchAsync(IReadOnlyList<JournalEntry> entries, CancellationToken cancellationToken = default)
        => _journalService.AppendBatchAsync(entries, cancellationToken);

    protected virtual void BeforeUpsertLocked(TEntity entity, TEntity? existing) { }

    protected TEntity CloneEntity(TEntity entity)
        => _descriptor.Clone(entity);

    protected JournalEntry CreateRemoveEntry(TKey key)
        => CreateEntry(++_stateStore.LastSequenceId, JournalEntityOperationType.Remove, _descriptor.SerializeKey(key));

    protected JournalEntry CreateUpsertEntry(TEntity entity)
        => CreateEntry(++_stateStore.LastSequenceId, JournalEntityOperationType.Upsert, _descriptor.SerializeEntity(entity));

    protected JournalEntry CreateUpsertEntry(long sequenceId, long timestampUnixMilliseconds, TEntity entity)
        => CreateEntry(
            sequenceId,
            timestampUnixMilliseconds,
            JournalEntityOperationType.Upsert,
            _descriptor.SerializeEntity(entity)
        );

    protected Dictionary<TKey, TEntity> GetBucket()
        => _stateStore.GetBucket<TEntity, TKey>(_descriptor.TypeId);

    protected virtual TEntity PrepareEntityForStore(TEntity entity)
        => _descriptor.Clone(entity);

    private JournalEntry CreateEntry(long sequenceId, JournalEntityOperationType operation, byte[] payload)
        => CreateEntry(sequenceId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), operation, payload);

    private JournalEntry CreateEntry(
        long sequenceId,
        long timestampUnixMilliseconds,
        JournalEntityOperationType operation,
        byte[] payload
    )
        => new()
        {
            SequenceId = sequenceId,
            TimestampUnixMilliseconds = timestampUnixMilliseconds,
            TypeId = _descriptor.TypeId,
            Operation = operation,
            Payload = payload
        };
}
