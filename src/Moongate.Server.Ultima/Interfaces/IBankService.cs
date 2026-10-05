using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Bank;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The players' bank boxes, as ModernUO's: a box worn on the bank layer, made the first time it is opened, which its
///     owner reaches only while it is open. It stays open while the player stands where it was opened: a step, a
///     teleport, a map change or a new login closes it. Called on the game loop.
/// </summary>
public interface IBankService : ISessionClosedListener
{
    /// <summary>
    ///     Opens the player's bank box and shows it, making it first when the player has none (it shows once saved);
    ///     false when the player has no session in the world.
    /// </summary>
    bool Open(MobileEntity player);

    /// <summary>
    ///     Closes the player's bank box, as a step does.
    /// </summary>
    void Close(MobileEntity player);

    /// <summary>
    ///     Gets whether the player's bank box is open: the same character on the spot where it opened it.
    /// </summary>
    bool IsOpen(MobileEntity player);

    /// <summary>
    ///     Gets whether <paramref name="character" /> may lift, drop into or use <paramref name="item" />: anything outside
    ///     a bank box; inside one, only its owner while it is open, or a game master or above.
    /// </summary>
    bool CanAccess(GameSession session, MobileEntity character, ItemEntity item);

    /// <summary>
    ///     Gets the gold in the player's bank: the coins anywhere inside its bank box, bags included; 0 for a player
    ///     with no bank box yet, null for an NPC.
    /// </summary>
    int? Balance(MobileEntity player);

    /// <summary>
    ///     Moves that many coins from the player's bank to its backpack, onto a pile already there when it fits. All
    ///     or nothing: a refusal moves nothing. The player need not have the box open. A backpack already at its weight
    ///     takes nothing; any other takes the gold whatever it weighs, as ModernUO.
    /// </summary>
    BankResultType Withdraw(MobileEntity player, int amount);

    /// <summary>
    ///     Moves that many coins from the player's backpack and its bags to its bank box, topping up the piles of the
    ///     box and then making piles of 60000. All or nothing: a refusal moves nothing.
    /// </summary>
    BankResultType Deposit(MobileEntity player, int amount);
}
