using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Tells the scripted ground items that a mobile walked onto them, as ModernUO's <c>OnMoveOver</c>: the item script
///     event <c>on_move_over(serial, mobile)</c>, which a teleporter answers by moving the mobile.
/// </summary>
/// <remarks>
///     Game loop only; call it after the step, once the players around were told of it.
/// </remarks>
public interface IMoveOverService
{
    /// <summary>
    ///     Runs <c>on_move_over</c>, or <c>on_npc_move_over</c> when the mobile is an NPC, of every scripted item lying on the mobile's cell at its height, or overlapping it as
    ///     in ModernUO: an item up to 14 above the mobile's feet, or one below them tall enough to reach them. Once a script
    ///     moved the mobile off the cell, the items left are not run.
    /// </summary>
    void SteppedOn(MobileEntity mobile);
}
