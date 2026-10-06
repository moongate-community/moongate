using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class PersonalLightLevelPacketTests
{
    [Fact]
    public void Encode_WritesTheExpectedBytes()
    {
        Assert.Equal(
            Convert.FromHexString("4E0000000205"),
            PacketCodec.Encode(new PersonalLightLevelPacket(new Serial(0x00000002), 5))
        );
    }
}
