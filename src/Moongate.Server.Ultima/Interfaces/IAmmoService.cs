using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The arrows and the bolts a player spends: one is taken out of the backpack for each shot, and some are found again
///     where they fell.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IAmmoService
{
    /// <summary>
    ///     Takes one arrow, for a bow, or one bolt, for a crossbow, out of the shooter's backpack, in a bag too, and shows
    ///     the stack smaller. False, and nothing taken, when it has none or the weapon shoots nothing.
    /// </summary>
    bool Spend(MobileEntity shooter, WeaponInfo weapon);

    /// <summary>
    ///     Maybe leaves a spent arrow or bolt on the ground at the target's feet, for whoever picks it up: 40 percent of the
    ///     shots, as ModernUO's recovery, hit or missed.
    /// </summary>
    void Recover(MobileEntity target, WeaponInfo weapon);
}
