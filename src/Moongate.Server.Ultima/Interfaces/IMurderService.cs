using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The murder counts, as ModernUO's: nothing is counted when a player dies, the victim reports the kill in a
///     gump. A report adds a long-term kill and a short-term murder to the killer; from five kills it is a murderer
///     and its name is red. Both counts are forgotten with time.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface IMurderService
{
    /// <summary>
    ///     Notes that <paramref name="attacker" /> went for an innocent player that did not fight it: if the victim dies
    ///     soon, it may report the attacker. Nothing for an NPC on either side.
    /// </summary>
    void Aggressed(MobileEntity attacker, MobileEntity victim);

    /// <summary>
    ///     Asks the player that died, a few seconds later, to report each of those who attacked it as a criminal, one gump
    ///     after the other.
    /// </summary>
    void Died(MobileEntity victim);

    /// <summary>
    ///     Counts a murder against <paramref name="killer" />: one more kill and short-term murder, its karma lowered,
    ///     both counts forgotten later. False, and nothing counted, for a killer that is no player in the world, or one
    ///     the victim reported a moment ago.
    /// </summary>
    bool Report(MobileEntity victim, MobileEntity killer);

    /// <summary>
    ///     Forgets the counts of a player whose time is over, as when it comes back into the world.
    /// </summary>
    void Restore(MobileEntity mobile);
}
