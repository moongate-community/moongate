using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates melee combat targeting and swing resolution.
/// </summary>
public interface ICombatService
{
    /// <summary>
    /// Tries to set the defender as the attacker's active combat target and schedule the first swing.
    /// </summary>
    Task<bool> TrySetCombatantAsync(Serial attackerId, Serial defenderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the attacker's active combat target and pending swing.
    /// </summary>
    Task<bool> ClearCombatantAsync(Serial attackerId, CancellationToken cancellationToken = default);
}
