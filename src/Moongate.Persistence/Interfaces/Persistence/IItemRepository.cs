using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Provides persistence operations for item entities.
/// </summary>
public interface IItemRepository : IBaseRepository<UOItemEntity, Serial>
{
    /// <summary>
    /// Inserts or updates multiple items in a single batched operation.
    /// </summary>
    ValueTask BulkUpsertAsync(IReadOnlyList<UOItemEntity> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a projection query over item entities.
    /// </summary>
    ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
        Func<UOItemEntity, bool> predicate,
        Func<UOItemEntity, TResult> selector,
        CancellationToken cancellationToken = default
    );
}
