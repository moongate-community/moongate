using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Items;

/// <summary>
/// Generates first-open loot for container items backed by loot template references.
/// </summary>
public interface ILootGenerationService
{
    /// <summary>
    /// Ensures first-open loot has been generated for the provided container.
    /// </summary>
    /// <param name="container">Container item being opened.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest hydrated container state after generation completes.</returns>
    Task<UOItemEntity> EnsureLootGeneratedAsync(
        UOItemEntity container,
        CancellationToken cancellationToken = default
    );
}
