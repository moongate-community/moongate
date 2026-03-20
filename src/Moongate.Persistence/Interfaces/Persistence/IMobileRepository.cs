using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Persistence.Interfaces.Persistence;

/// <summary>
/// Provides persistence operations for mobile entities.
/// </summary>
public interface IMobileRepository : IBaseRepository<UOMobileEntity, Serial>
{
    /// <summary>
    /// Runs a projection query over mobile entities.
    /// </summary>
    ValueTask<IReadOnlyList<TResult>> QueryAsync<TResult>(
        Func<UOMobileEntity, bool> predicate,
        Func<UOMobileEntity, TResult> selector,
        CancellationToken cancellationToken = default
    );
}
