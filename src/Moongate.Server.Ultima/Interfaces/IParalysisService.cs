using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Paralyzes mobiles: a paralyzed mobile is frozen (it neither steps, turns nor casts) until its time is up. The time
///     is saved with the mobile as a prop, so a player that comes back after a restart is freed at the right moment.
/// </summary>
public interface IParalysisService
{
    /// <summary>
    ///     Freezes the mobile for <paramref name="duration" />; false, with nothing changed, for one already frozen (a
    ///     paralysis does not stack and does not extend) or dead, or a duration that is not positive.
    /// </summary>
    bool Paralyze(MobileEntity mobile, TimeSpan duration);

    /// <summary>
    ///     Gets whether the mobile is frozen by a paralysis that has not ended.
    /// </summary>
    bool IsParalyzed(MobileEntity mobile);

    /// <summary>
    ///     Ends the mobile's paralysis and frees it; false when it was not paralyzed.
    /// </summary>
    bool Release(MobileEntity mobile);

    /// <summary>
    ///     Takes a saved paralysis up again, as a paralyzed player comes back: ended when its time passed, else timed for
    ///     what is left.
    /// </summary>
    void Resume(MobileEntity mobile);
}
