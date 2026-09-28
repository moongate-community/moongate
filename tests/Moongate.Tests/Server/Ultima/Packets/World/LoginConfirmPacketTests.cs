using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class LoginConfirmPacketTests
{
    [Fact]
    public void Encode_WritesThePlayerAndTheMapSize()
    {
        var packet = new LoginConfirmPacket(new Serial(0x00000002), 0x0191, new Point3D(1496, 1628, -5), DirectionType.South, 7168, 4096);

        Assert.Equal(
            Convert.FromHexString(
                "1B" + "00000002" + "00000000" + "0191" + "05D8" + "065C" + "FFFB" + "04" + "00" + "FFFFFFFF" + "00000000" +
                "1C00" + "1000" + "000000000000"
            ),
            PacketCodec.Encode(packet)
        );
    }
}
