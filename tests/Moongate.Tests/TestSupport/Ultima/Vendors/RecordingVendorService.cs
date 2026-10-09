using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Vendors;

namespace Moongate.Tests.TestSupport.Ultima.Vendors;

/// <summary>
///     Records the windows opened and the replies handed to the vendor service; opens every window unless told not to.
/// </summary>
public sealed class RecordingVendorService : IVendorService
{
    public bool Answer { get; set; } = true;

    public bool HasGoods { get; set; } = true;

    public bool WantsGoods { get; set; } = true;

    public List<(GameSession Session, MobileEntity Vendor)> Opened { get; } = [];

    public List<(GameSession Session, VendorBuyReplyPacket Packet)> Replies { get; } = [];

    public List<(GameSession Session, MobileEntity Vendor)> OpenedSell { get; } = [];

    public List<(GameSession Session, VendorSellReplyPacket Packet)> SellReplies { get; } = [];

    public bool Sells(MobileEntity vendor)
    {
        return HasGoods;
    }

    public bool Buys(MobileEntity vendor)
    {
        return WantsGoods;
    }

    public bool OpenBuy(GameSession session, MobileEntity vendor)
    {
        Opened.Add((session, vendor));

        return Answer;
    }

    public void Buy(GameSession session, VendorBuyReplyPacket packet)
    {
        Replies.Add((session, packet));
    }

    public bool OpenSell(GameSession session, MobileEntity vendor)
    {
        OpenedSell.Add((session, vendor));

        return Answer;
    }

    public void Sell(GameSession session, VendorSellReplyPacket packet)
    {
        SellReplies.Add((session, packet));
    }

    public void Close(GameSession session)
    {
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
