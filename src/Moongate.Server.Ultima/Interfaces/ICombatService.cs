using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Melee combat, as ModernUO's classic one: a mobile fights one target, swings by a timer of its stamina, hits by its
///     Wrestling against the target's and does damage with tactics, strength and anatomy, less the armor of the target. A
///     player never drops below one hit point yet; an NPC at none dies.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ICombatService : IMoongateStartupService
{
    /// <summary>
    ///     Makes <paramref name="attacker" /> fight <paramref name="target" />, a player going into war mode and being told
    ///     whom it fights (0xAA). The first swing comes at once, and one that hits an innocent who is not fighting it makes
    ///     a player a criminal. False, with nothing changed, when either is not in the world or they are on another map,
    ///     the target is the attacker, the target is hidden from the attacker, out of its view or out of its sight.
    /// </summary>
    bool Attack(MobileEntity attacker, MobileEntity target);

    /// <summary>
    ///     Hurts <paramref name="target" /> by <paramref name="damage" /> without a swing, as an explosion does: the rules of
    ///     a blow (a crime against an innocent, the murder report, the hurt sound and gesture, the damage shown, a death
    ///     with its killer, an NPC fighting back) without war mode. False for an invulnerable, dead or absent target, or a
    ///     negative damage.
    /// </summary>
    bool Harm(MobileEntity? attacker, MobileEntity target, int damage);

    /// <summary>
    ///     Makes <paramref name="attacker" /> the aggressor of <paramref name="target" /> without a blow or a hurt, as a curse
    ///     is: a player that curses an innocent who is not fighting it is a criminal and the murder report is told, and an
    ///     NPC that is cursed fights back. False for a target that is dead, invulnerable or not in the world.
    /// </summary>
    bool Aggress(MobileEntity attacker, MobileEntity target);

    /// <summary>
    ///     Ends the fight of <paramref name="mobile" />: it swings no more, and its player is told it fights no one.
    /// </summary>
    void Stop(MobileEntity mobile);

    /// <summary>
    ///     Gets whom <paramref name="mobile" /> fights; null when it fights no one.
    /// </summary>
    MobileEntity? TargetOf(MobileEntity mobile);

    /// <summary>
    ///     Gets how far the mobile's blows reach, in cells: the range of the bow or crossbow an NPC holds, else the
    ///     melee range of the configuration.
    /// </summary>
    int RangeOf(MobileEntity mobile);

    /// <summary>
    ///     Gets the weapon the mobile holds: the bow or crossbow of a player or an NPC, else the melee weapon of a player;
    ///     null for fists and for an NPC with no bow.
    /// </summary>
    WeaponInfo? HeldWeaponOf(MobileEntity mobile);

    /// <summary>
    ///     Turns the mobile towards <paramref name="x" />, <paramref name="y" /> and plays the swing of what it holds,
    ///     seen by the players in range, as a swing of a fight does, without a fight, a hit or a cost.
    /// </summary>
    void PlaySwing(MobileEntity mobile, int x, int y);

    /// <summary>
    ///     Takes one arrow or bolt out of the backpack of a mobile that holds a bow or a crossbow, as a shot does. False,
    ///     and nothing taken, when it holds none or has no ammunition.
    /// </summary>
    bool SpendAmmo(MobileEntity shooter);
}
