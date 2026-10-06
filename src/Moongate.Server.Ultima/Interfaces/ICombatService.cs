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
}
