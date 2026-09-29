using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class ViewRangePacketTests
{
    [Fact]
    public void Encode_WritesTheRange()
    {
        Assert.Equal(Convert.FromHexString("C812"), PacketCodec.Encode(new ViewRangePacket(18)));
    }
}
