using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Handlers.Vendors;
using Moongate.Server.Ultima.Packets.Vendors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Vendors;

namespace Moongate.Tests.Server.Ultima.Handlers.Vendors;

public sealed class VendorBuyReplyPacketHandlerTests
{
    [Fact]
    public async Task Handle_GivesTheReplyOfTheSessionToTheVendorService()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var session = await fixture.AddAsync(2);
        var vendors = new RecordingVendorService();
        Assert.True(
            VendorBuyReplyPacket.TryParse(Convert.FromHexString("3B0008" + "00000064" + "00"), out var packet)
        );

        new VendorBuyReplyPacketHandler(vendors).Handle(session, packet);

        Assert.Equal((session, packet), Assert.Single(vendors.Replies));
    }
}
