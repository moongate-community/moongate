using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class CurrentTimePacketTests
{
    [Fact]
    public void Encode_WritesTheExpectedBytes()
    {
        Assert.Equal(Convert.FromHexString("5B0D0509"), PacketCodec.Encode(new CurrentTimePacket(new TimeOnly(13, 5, 9))));
    }
}
