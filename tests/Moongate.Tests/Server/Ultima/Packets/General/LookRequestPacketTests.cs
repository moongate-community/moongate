using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class LookRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheSerial()
    {
        Assert.True(LookRequestPacket.TryParse(Convert.FromHexString("09" + "40000010"), out var packet));

        Assert.Equal(new Serial(0x40000010), packet.Target);
    }
}
