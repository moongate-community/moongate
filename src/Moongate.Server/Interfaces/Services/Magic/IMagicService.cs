using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Magic;

/// <summary>
/// Orchestrates mobile spell casting state and cast completion.
/// </summary>
public interface IMagicService
{
    /// <summary>
    /// Returns whether the specified mobile is currently casting a spell.
    /// </summary>
    /// <param name="casterId">Caster serial identifier.</param>
    bool IsCasting(Serial casterId);

    /// <summary>
    /// Attempts to begin a spell cast for the supplied mobile.
    /// </summary>
    /// <param name="caster">Mobile attempting the cast.</param>
    /// <param name="spellId">Registered spell identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> TryCastAsync(
        UOMobileEntity caster,
        int spellId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Interrupts an in-progress cast for the specified mobile.
    /// </summary>
    /// <param name="casterId">Caster serial identifier.</param>
    void Interrupt(Serial casterId);

    /// <summary>
    /// Handles expiry of a cast-delay timer for the specified mobile.
    /// </summary>
    /// <param name="casterId">Caster serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask OnCastTimerExpiredAsync(Serial casterId, CancellationToken cancellationToken = default);
}
