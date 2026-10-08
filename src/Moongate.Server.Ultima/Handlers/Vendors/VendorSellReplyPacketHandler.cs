using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Vendors;

namespace Moongate.Server.Ultima.Handlers.Vendors;

/// <summary>
///     Hands what a player chose to sell (0x9F) to the vendor service, which buys it or refuses it whole.
/// </summary>
public sealed class VendorSellReplyPacketHandler : IPacketHandler<VendorSellReplyPacket>
{
    private readonly IVendorService _vendors;

    public VendorSellReplyPacketHandler(IVendorService vendors)
    {
        _vendors = vendors;
    }

    public void Handle(GameSession session, VendorSellReplyPacket packet)
    {
        _vendors.Sell(session, packet);
    }
}
