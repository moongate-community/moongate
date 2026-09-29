using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class UseRequestPacketTests
{
    [Fact]
    public void TryParse_ReadsTheSerialOfTheUsedObject()
    {
        Assert.True(UseRequestPacket.TryParse(Convert.FromHexString("0640000001"), out var packet));

        Assert.Equal(new Serial(0x40000001), packet.Target);
    }
}
