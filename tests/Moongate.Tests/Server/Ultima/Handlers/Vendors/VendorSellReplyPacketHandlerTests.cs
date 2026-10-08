using Moongate.Server.Ultima.Handlers.Vendors;
using Moongate.Server.Ultima.Packets.Vendors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Vendors;

namespace Moongate.Tests.Server.Ultima.Handlers.Vendors;

public sealed class VendorSellReplyPacketHandlerTests
{
    [Fact]
    public async Task Handle_GivesTheReplyOfTheSessionToTheVendorService()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var session = await fixture.AddAsync(2);
        var vendors = new RecordingVendorService();
        Assert.True(
            VendorSellReplyPacket.TryParse(Convert.FromHexString("9F0009" + "00000064" + "0000"), out var packet)
        );

        new VendorSellReplyPacketHandler(vendors).Handle(session, packet);

        Assert.Equal((session, packet), Assert.Single(vendors.SellReplies));
    }
}
