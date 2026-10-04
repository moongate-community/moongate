using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Gives the mobiles their hit points, mana and stamina back with time, one point at a time, as ModernUO's classic
///     rules: only the interval changes. The players in the world are ticked every second; an NPC is ticked by its
///     think, so one that sleeps far from every player does not regenerate. Called on the game loop.
/// </summary>
public interface IRegenerationService
{
    /// <summary>
    ///     Looks at the three bars of the mobile: one below its maximum starts its wait, and gains a point when the
    ///     wait is over. A player with an empty stomach gets no hit points back.
    /// </summary>
    void Tick(MobileEntity mobile);

    /// <summary>
    ///     Gets the seconds between two points of mana of the mobile, by its intelligence and its Meditation.
    /// </summary>
    double ManaSeconds(MobileEntity mobile);
}
