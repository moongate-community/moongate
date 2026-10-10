using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Keeps the timed effects a mobile is under, such as the bonus of a strength potion or the night sight of a night
///     sight potion: they end when their time is up or when the player leaves, and they are never saved.
/// </summary>
public interface IStatBonusService : IRegionChangeListener
{
    /// <summary>
    ///     Raises a stat by <paramref name="amount" /> for <paramref name="duration" />; false when the mobile has a bonus of
    ///     that stat already, or for an amount or a duration that is not positive.
    /// </summary>
    bool TryAddBonus(MobileEntity mobile, StatBonusType stat, int amount, TimeSpan duration);

    /// <summary>
    ///     Gets the bonus of a stat the mobile is under; 0 for none.
    /// </summary>
    int Bonus(MobileEntity mobile, StatBonusType stat);

    /// <summary>
    ///     Gives a player a personal light of <paramref name="level" /> (0 to 30) for <paramref name="duration" />; false
    ///     when it has night sight already, or for a level or a duration out of range.
    /// </summary>
    bool TrySetNightSight(MobileEntity mobile, int level, TimeSpan duration);

    /// <summary>
    ///     Gets whether the mobile has night sight.
    /// </summary>
    bool HasNightSight(MobileEntity mobile);

    /// <summary>
    ///     Ends every timed effect of the mobile at once, with the hits and stamina above the base maximums, as the player
    ///     leaves: before its save, so none of them is written.
    /// </summary>
    void EndAll(MobileEntity mobile);
}
