using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The day and night light of the players, as ModernUO's light cycle: every 5 seconds each player in the world whose
///     level changed is sent the new one (0x4F). Dungeon and jail regions have their own level, sent as soon as a player
///     walks in or out.
/// </summary>
public interface ILightService : IMoongateStartupService, IRegionChangeListener
{
    /// <summary>
    ///     Gets the level every player is given instead of the clock's, set by <c>.globallight</c>; null follows the
    ///     clock. It is not saved.
    /// </summary>
    int? Override { get; }

    /// <summary>
    ///     Gets the light level where <paramref name="mobile" /> stands: the override when set; else the dungeon or jail
    ///     level in such a region; else night from 00:00 to 03:59, brightening until 06:00, day until 21:59, darkening until
    ///     midnight.
    /// </summary>
    int LevelFor(MobileEntity mobile);

    /// <summary>
    ///     Gets the level for a character entering the world and records it as sent, so the cycle resends it only when it
    ///     changes.
    /// </summary>
    int LevelOnLogin(MobileEntity character);

    /// <summary>
    ///     Sets or, with null, clears the override, and sends every player in the world its level at once. Call it on the
    ///     game loop, as a script does; <see cref="SetOverrideAsync" /> is for the rest.
    /// </summary>
    void SetOverride(int? level);

    /// <summary>
    ///     Does what <see cref="SetOverride" /> does, on the game loop, and completes when it is done.
    /// </summary>
    Task SetOverrideAsync(int? level, CancellationToken cancellationToken = default);
}
