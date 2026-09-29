using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class LiftRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheItemAndTheAmount()
    {
        Assert.True(LiftRequestPacket.TryParse(Convert.FromHexString("07400000120019"), out var packet));

        Assert.Equal((new Serial(0x40000012), 25), (packet.Item, packet.Amount));
    }
}
