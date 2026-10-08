using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.Vendors;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The shop window of the NPC vendors: opening it, buying from it, closing it. Game loop only. The stock of a vendor
///     lives in memory and starts full again at a restart, as ModernUO's.
/// </summary>
public interface IVendorService : ISessionClosedListener
{
    /// <summary>
    ///     Opens the buy window of <paramref name="vendor" /> for the player of <paramref name="session" />, replacing the
    ///     one it had. Refused, with nothing sent, when the vendor has no shop or nothing in stock, is not in the world, is
    ///     more than 10 tiles away or out of sight, or when the player is dead; a murderer in a guarded region is
    ///     refused by the vendor's voice.
    /// </summary>
    /// <returns>
    ///     True when the window opened.
    /// </returns>
    bool OpenBuy(GameSession session, MobileEntity vendor);

    /// <summary>
    ///     Carries out what the player chose in the window: everything or nothing. Whatever the outcome, the window ends.
    ///     A reply that is not for the open window, or has more than 100 lines, is dropped.
    /// </summary>
    void Buy(GameSession session, VendorBuyReplyPacket packet);

    /// <summary>
    ///     Offers the player's items that the shop of <paramref name="vendor" /> buys, in a sell list of 250 items at most.
    ///     Refused, with nothing sent, on the grounds of <see cref="OpenBuy" />; a player with nothing to sell is told so by
    ///     the vendor. The items are those in the backpack and its bags, not worn or held, movable, and empty when they are
    ///     containers.
    /// </summary>
    /// <returns>True when the list was sent.</returns>
    bool OpenSell(GameSession session, MobileEntity vendor);

    /// <summary>
    ///     Carries out what the player chose in the sell list: everything or nothing. Whatever the outcome, the list ends.
    ///     A reply that is not for the open list of that vendor, or has 100 lines or more, is dropped.
    /// </summary>
    void Sell(GameSession session, VendorSellReplyPacket packet);

    /// <summary>
    ///     Forgets the open window and sell list of the session, if any.
    /// </summary>
    void Close(GameSession session);
}
