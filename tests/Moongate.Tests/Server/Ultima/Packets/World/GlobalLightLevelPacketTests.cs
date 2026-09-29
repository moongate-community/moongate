using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class GlobalLightLevelPacketTests
{
    [Fact]
    public void Encode_WritesTheExpectedBytes()
    {
        Assert.Equal(Convert.FromHexString("4F0C"), PacketCodec.Encode(new GlobalLightLevelPacket(12)));
    }
}
