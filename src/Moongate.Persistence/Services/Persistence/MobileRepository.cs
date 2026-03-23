using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Persistence.Services.Persistence;

/// <summary>
/// Thread-safe mobile repository backed by the shared persistence state store.
/// </summary>
internal sealed class MobileRepository : BaseRepository<UOMobileEntity, Serial>, IMobileRepository
{
    private readonly ILogger _logger = Log.ForContext<MobileRepository>();

    internal MobileRepository(
        PersistenceStateStore stateStore,
        IJournalService journalService,
        IPersistenceEntityDescriptor<UOMobileEntity, Serial> descriptor
    )
        : base(stateStore, journalService, descriptor) { }

    public ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
        Func<UOMobileEntity, bool> predicate,
        Func<UOMobileEntity, TResult> selector,
        CancellationToken cancellationToken = default
    )
    {
        _logger.Verbose("Mobile query requested");
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(selector);

        var results = new List<TResult>();

        lock (_stateStore.SyncRoot)
        {
            foreach (var mobile in _stateStore.MobilesById.Values)
            {
                if (!predicate(mobile))
                {
                    continue;
                }

                results.Add(selector(CloneEntity(mobile)));
            }
        }

        _logger.Verbose("Mobile query completed with Count={Count}", results.Count);

        return ValueTask.FromResult<IReadOnlyList<TResult>>(results);
    }

    protected override void BeforeUpsertLocked(UOMobileEntity entity, UOMobileEntity? existing)
        => _stateStore.LastMobileId = Math.Max(_stateStore.LastMobileId, (uint)entity.Id);
}
