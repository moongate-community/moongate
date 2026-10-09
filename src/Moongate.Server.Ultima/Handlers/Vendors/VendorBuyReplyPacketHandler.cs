using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Vendors;

namespace Moongate.Server.Ultima.Handlers.Vendors;

/// <summary>
///     Hands what a player chose in a vendor's shop window (0x3B) to the vendor service, which buys it or refuses it
///     whole.
/// </summary>
public sealed class VendorBuyReplyPacketHandler : IPacketHandler<VendorBuyReplyPacket>
{
    private readonly IVendorService _vendors;

    public VendorBuyReplyPacketHandler(IVendorService vendors)
    {
        _vendors = vendors;
    }

    public void Handle(GameSession session, VendorBuyReplyPacket packet)
    {
        _vendors.Buy(session, packet);
    }
}
