using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Gives each NPC near a player a repeating think on the timer wheel, as ModernUO's AITimer; a sleeping NPC has no
///     timer and costs nothing. Called on the game loop.
/// </summary>
public interface INpcTickService
{
    /// <summary>
    ///     How many NPCs have a think timer.
    /// </summary>
    int AwakeCount { get; }

    /// <summary>
    ///     How many thinks ran since the start.
    /// </summary>
    long ThinkCount { get; }

    /// <summary>
    ///     Whether the NPC with <paramref name="serial" /> has a think timer.
    /// </summary>
    bool IsAwake(Serial serial);

    /// <summary>
    ///     Starts the NPC's think timer; does nothing for a player or an NPC already awake.
    /// </summary>
    void Wake(MobileEntity npc);

    /// <summary>
    ///     Stops the NPC's think timer; does nothing when it is asleep.
    /// </summary>
    void Sleep(MobileEntity npc);
}
