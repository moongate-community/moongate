using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class RemoveEntityPacketTests
{
    [Fact]
    public void Encode_WritesTheSerial()
    {
        Assert.Equal(
            Convert.FromHexString("1D40000012"),
            PacketCodec.Encode(new RemoveEntityPacket(new Serial(0x40000012)))
        );
    }
}
