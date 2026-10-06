using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Combat;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What a mobile wields and wears for a fight, read from the templates of the items it has on: the weapon in its hands
///     and the armor of each part of its body.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ICombatGearService
{
    /// <summary>
    ///     Gets the melee weapon the mobile holds, the first item on its hand layers with a damage of its template; null for
    ///     none, for a shield and for a bow or a thrown weapon, which are not fought with yet: such a mobile fights with
    ///     its fists.
    /// </summary>
    WeaponInfo? WeaponOf(MobileEntity mobile);

    /// <summary>
    ///     Gets the armor rating of the piece the mobile wears on a part of its body, the best of the layers of that part;
    ///     0 for none.
    /// </summary>
    int ArmorAt(MobileEntity mobile, ArmorZoneType zone);

    /// <summary>
    ///     Gets the armor rating of the whole mobile: the armor of each part, weighted by how often a blow lands on it
    ///     (neck and hands 7%, arms 14%, head 15%, legs 22%, chest 35%), as the status window shows it.
    /// </summary>
    int ArmorRatingOf(MobileEntity mobile);

    /// <summary>
    ///     Gives the status of a player the damage of its weapon, with its bonuses, and its armor rating, in place of
    ///     those of its fists and none.
    /// </summary>
    MobileStatusInfo WithGear(MobileStatusInfo status, MobileEntity mobile);
}
