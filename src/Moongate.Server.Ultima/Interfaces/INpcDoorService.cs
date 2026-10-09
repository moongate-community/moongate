using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The doors an NPC opens on its way, as ModernUO's creatures that can open doors. Game loop only.
/// </summary>
public interface INpcDoorService
{
    /// <summary>
    ///     Gets whether <paramref name="npc" /> opens the doors in its way: what its mobile template says with
    ///     <c>opens_doors</c>, else a human or a monster body does and an animal or a sea creature does not.
    /// </summary>
    /// <returns>
    ///     True when it opens doors; false for a player.
    /// </returns>
    bool OpensDoors(MobileEntity npc);

    /// <summary>
    ///     Opens the door standing on the cell <paramref name="npc" /> faces in <paramref name="direction" />, when it
    ///     opens doors and the door is closed, not locked, at its height and scripted. The door's script runs
    ///     <c>on_npc_use(serial, npc)</c> on a later turn of the loop, so the door is open by the NPC's next step.
    /// </summary>
    /// <returns>
    ///     True when a door was asked to open.
    /// </returns>
    bool TryOpen(MobileEntity npc, DirectionType direction);
}
