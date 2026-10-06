using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MobileUpdatePacketTests
{
    [Fact]
    public void Encode_WritesBodyHueFlagsLocationAndDirection()
    {
        var packet = new MobileUpdatePacket(
            new Serial(0x00000002),
            new Body(0x0191),
            new Hue(0x83EA),
            MobileFlagsType.Female | MobileFlagsType.WarMode,
            new Point3D(1496, 1628, -5),
            DirectionType.South
        );

        Assert.Equal(
            Convert.FromHexString(
                "20" + "00000002" + "0191" + "00" + "83EA" + "42" + "05D8" + "065C" + "0000" + "04" + "FB"
            ),
            PacketCodec.Encode(packet)
        );
    }
}
