using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates death resolution for mobiles and corpse creation for lootable NPCs.
/// </summary>
public interface IDeathService
{
    /// <summary>
    /// Forces a mobile into a lethal state and resolves the death pipeline.
    /// </summary>
    /// <param name="victim">Mobile to kill.</param>
    /// <param name="killer">Optional killer mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the death was handled; otherwise <c>false</c>.</returns>
    Task<bool> ForceDeathAsync(
        UOMobileEntity victim,
        UOMobileEntity? killer,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resolves a mobile death once hit points are depleted.
    /// </summary>
    /// <param name="victim">Mobile that died.</param>
    /// <param name="killer">Optional killer mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the death was handled; otherwise <c>false</c>.</returns>
    Task<bool> HandleDeathAsync(
        UOMobileEntity victim,
        UOMobileEntity? killer,
        CancellationToken cancellationToken = default
    );
}
