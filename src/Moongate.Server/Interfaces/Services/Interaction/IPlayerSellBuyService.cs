using Moongate.Network.Packets.Incoming.Trading;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Orchestrates the classic vendor buy/sell flow between a player and an NPC vendor.
/// </summary>
public interface IPlayerSellBuyService
{
    /// <summary>
    /// Handles the inbound vendor buy confirmation packet and applies the purchase transaction.
    /// </summary>
    Task HandleBuyItemsAsync(long sessionId, BuyItemsPacket packet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the inbound vendor sell reply packet and applies the sell transaction.
    /// </summary>
    Task HandleSellListReplyAsync(long sessionId, SellListReplyPacket packet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the classic vendor buy UI for the specified player session and vendor.
    /// </summary>
    Task HandleVendorBuyRequestAsync(long sessionId, Serial vendorSerial, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the classic vendor sell UI for the specified player session and vendor.
    /// </summary>
    Task HandleVendorSellRequestAsync(long sessionId, Serial vendorSerial, CancellationToken cancellationToken = default);
}
