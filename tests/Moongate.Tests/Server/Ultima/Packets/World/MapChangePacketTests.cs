using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MapChangePacketTests
{
    [Fact]
    public void Encode_WritesTheGeneralInfoSubcommand()
    {
        Assert.Equal(
            new byte[] { 0xBF, 0x00, 0x06, 0x00, 0x08, 0x01 },
            PacketCodec.Encode(new MapChangePacket(MapType.Trammel))
        );
    }
}
