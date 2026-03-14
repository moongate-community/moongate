using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Drives runtime NPC spawning from active spawner items.
/// </summary>
public interface ISpawnService : IMoongateService
{
    /// <summary>
    /// Returns the number of tracked runtime spawner states.
    /// </summary>
    int GetTrackedSpawnerCount();

    /// <summary>
    /// Forces a spawn attempt for a specific spawner item, bypassing runtime activation checks.
    /// </summary>
    /// <param name="spawnerItemId">Spawner item serial.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when a mobile is spawned; otherwise <see langword="false" />.</returns>
    Task<bool> TriggerAsync(Serial spawnerItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a spawn attempt for a persisted spawner item, bypassing active-sector discovery.
    /// </summary>
    /// <param name="spawnerItem">Persisted spawner item entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when a mobile is spawned; otherwise <see langword="false" />.</returns>
    Task<bool> TriggerAsync(UOItemEntity spawnerItem, CancellationToken cancellationToken = default);
}
