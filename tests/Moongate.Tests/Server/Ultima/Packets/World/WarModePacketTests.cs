using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class WarModePacketTests
{
    [Fact]
    public void Encode_WritesTheExpectedBytes()
    {
        Assert.Equal(Convert.FromHexString("7201003200"), PacketCodec.Encode(new WarModePacket(true)));
    }
}
