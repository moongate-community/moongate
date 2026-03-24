using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Thread-safe item repository backed by the shared persistence state store.
/// </summary>
internal sealed class ItemRepository : BaseRepository<UOItemEntity, Serial>, IItemRepository
{
    private readonly ILogger _logger = Log.ForContext<ItemRepository>();

    internal ItemRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<UOItemEntity, Serial> descriptor
    )
        : base(stateStore, journalService, descriptor) { }

    public async ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        _logger.Verbose("Item bulk upsert requested for Count={Count}", items.Count);
        var clones = new List<UOItemEntity>(items.Count);
        long baseSequenceId;

        lock (_stateStore.SyncRoot)
        {
            var bucket = GetBucket();

            foreach (var item in items)
            {
                var clone = PrepareEntityForStore(item);
                bucket.TryGetValue(clone.Id, out var existing);
                BeforeUpsertLocked(clone, existing);
                bucket[clone.Id] = clone;
                clones.Add(clone);
            }

            baseSequenceId = _stateStore.LastSequenceId;
            _stateStore.LastSequenceId += clones.Count;
        }

        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entries = new List<JournalEntry>(clones.Count);

        for (var i = 0; i < clones.Count; i++)
        {
            entries.Add(CreateUpsertEntry(baseSequenceId + i + 1, timestampMs, clones[i]));
        }

        await AppendBatchAsync(entries, cancellationToken);
        _logger.Verbose("Item bulk upsert completed for Count={Count}", items.Count);
    }

    public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
        Func<UOItemEntity, bool> predicate,
        Func<UOItemEntity, TResult> selector,
        CancellationToken cancellationToken = default
    )
    {
        _logger.Verbose("Item query requested");
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(selector);

        var results = new List<TResult>();

        lock (_stateStore.SyncRoot)
        {
            foreach (var item in _stateStore.ItemsById.Values)
            {
                if (!predicate(item))
                {
                    continue;
                }

                results.Add(selector(CloneEntity(item)));
            }
        }

        _logger.Verbose("Item query completed with Count={Count}", results.Count);

        return ValueTask.FromResult<IReadOnlyList<TResult>>(results);
    }

    protected override void BeforeUpsertLocked(UOItemEntity entity, UOItemEntity? existing)
        => _stateStore.LastItemId = Math.Max(_stateStore.LastItemId, (uint)entity.Id);
}
