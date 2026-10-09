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
    ///     Gets the gold in the player's bank: the coins and the worth of the checks anywhere inside its bank box, bags
    ///     included; 0 for a player
    ///     with no bank box yet, null for an NPC.
    /// </summary>
    int? Balance(MobileEntity player);

    /// <summary>
    ///     Moves that many coins from the player's bank to its backpack, onto a pile already there when it fits; when
    ///     the coins of the bank are not enough the checks give the rest, the last one keeping what is left of it. All
    ///     or nothing: a refusal moves nothing. The player need not have the box open. A backpack already at its weight
    ///     takes nothing; any other takes the gold whatever it weighs, as ModernUO.
    /// </summary>
    BankResultType Withdraw(MobileEntity player, int amount);

    /// <summary>
    ///     Moves that many coins from the player's backpack and its bags to its bank box, topping up the piles of the
    ///     box and then making piles of 60000. All or nothing: a refusal moves nothing.
    /// </summary>
    BankResultType Deposit(MobileEntity player, int amount);

    /// <summary>
    ///     Writes a bank check worth <paramref name="amount" />, between the two bounds of the settings, paid with the
    ///     coins of the player's bank and put in its bank box. All or nothing.
    /// </summary>
    BankResultType WriteCheck(MobileEntity player, int amount);

    /// <summary>
    ///     Turns a bank check lying inside the player's bank box, at any depth, into coins of the box: the piles there
    ///     are topped up, then piles of 60000 are made. A box with room for part of it takes what fits and the check
    ///     keeps the rest; <paramref name="deposited" /> is what went in.
    /// </summary>
    BankResultType Cash(MobileEntity player, ItemEntity check, out int deposited);

    /// <summary>
    ///     Puts a gold pile or a bank check into the bank box of <paramref name="player" />, as when it is handed to a
    ///     banker: the gold tops up the piles of the box and what is left is a pile of its own; a check goes in worth
    ///     the same. All or nothing. The item is the player's, or nobody's (on the ground), and on no cursor.
    /// </summary>
    BankResultType DepositItem(MobileEntity player, ItemEntity item);

    /// <summary>
    ///     Pays <paramref name="amount" /> gold to the player, as a vendor does: piles of 60000 at most, put in the
    ///     backpack, or in the bank box when the backpack has no room for them. All or nothing: <c>BackpackFull</c>
    ///     when
    ///     neither has the room, <c>Busy</c> when no serial is ready or the player's items are reserved by another
    ///     operation, <c>BadAmount</c> under 1.
    /// </summary>
    BankResultType GiveGold(MobileEntity player, int amount);

    /// <summary>
    ///     Gets the coins the player carries: the gold piles of its backpack and of the bags inside it, not the ones on
    ///     a cursor.
    /// </summary>
    long CarriedGold(MobileEntity player);

    /// <summary>
    ///     Pays <paramref name="amount" /> gold out of what the player has, as a vendor is paid: the coins of the
    ///     backpack and its bags first, smallest piles first, then, when <paramref name="useBank" /> is true, the coins of
    ///     the bank box and then its checks. Everything is checked before anything moves, so it is all or nothing, and
    ///     none of the limits of a withdrawal applies: the gold does not pass through the backpack. <paramref name="fromBank" />
    ///     is how much came out of the bank box, to tell the player. <c>NotEnoughGold</c> when what the player has, in the
    ///     places allowed, is less; <c>Busy</c> when its items are reserved by another operation; <c>BadAmount</c> under 1.
    /// </summary>
    BankResultType Pay(MobileEntity player, int amount, bool useBank, out int fromBank);

    /// <summary>
    ///     Gets what a bank check is worth; null for an item that is not one.
    /// </summary>
    long? WorthOf(ItemEntity item);
}
