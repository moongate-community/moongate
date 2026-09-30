using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The day and night light of the players, as ModernUO's light cycle: every 5 seconds each player in the world whose
///     level changed is sent the new one (0x4F).
/// </summary>
public interface ILightService : IMoongateStartupService
{
    /// <summary>
    ///     Gets the level every player is given instead of the clock's, set by <c>.globallight</c>; null follows the
    ///     clock. It is not saved.
    /// </summary>
    int? Override { get; }

    /// <summary>
    ///     Gets the light level where <paramref name="mobile" /> stands: night from 00:00 to 03:59, brightening until 06:00,
    ///     day until 21:59, darkening until midnight; the override when set.
    /// </summary>
    int LevelFor(MobileEntity mobile);

    /// <summary>
    ///     Gets the level for a character entering the world and records it as sent, so the cycle resends it only when it
    ///     changes.
    /// </summary>
    int LevelOnLogin(MobileEntity character);

    /// <summary>
    ///     Sets or, with null, clears the override, and sends every player in the world its level at once.
    /// </summary>
    Task SetOverrideAsync(int? level, CancellationToken cancellationToken = default);
}
