using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What moving costs a player in stamina, as ModernUO counts it: running tires a little, carrying more than the
///     maximum tires at every step, and with no stamina left an overloaded player does not move and any player does
///     not run. The staff and NPCs are left alone. Called on the game loop.
/// </summary>
public interface IFatigueService
{
    /// <summary>
    ///     Gets whether the player may take the step; a refusal tells it why.
    /// </summary>
    bool CanStep(GameSession session, MobileEntity mobile, bool running);

    /// <summary>
    ///     Takes the stamina of a step the player took.
    /// </summary>
    void Stepped(GameSession session, MobileEntity mobile, bool running);

    /// <summary>
    ///     Shows the player what it carries now, after it lifted or put down an item, and with
    ///     <paramref name="warn" /> tells it when that is more than it may carry.
    /// </summary>
    void LoadChanged(GameSession session, MobileEntity mobile, bool warn);
}
