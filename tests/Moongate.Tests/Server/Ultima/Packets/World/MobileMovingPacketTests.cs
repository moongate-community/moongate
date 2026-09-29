using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MobileMovingPacketTests
{
    [Fact]
    public void Encode_WritesTheSeventeenBytes()
    {
        var packet = new MobileMovingPacket(
            new Serial(0x00000002),
            new Body(0x0191),
            new Point3D(1496, 1628, 10),
            DirectionType.East,
            false,
            new Hue(0x83EA),
            MobileFlagsType.Female,
            NotorietyType.Innocent
        );

        Assert.Equal(Convert.FromHexString("7700000002019105D8065C0A0283EA0201"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Encode_Running_SetsTheHighBitOfTheDirection()
    {
        var packet = new MobileMovingPacket(
            new Serial(0x00000002),
            new Body(0x0191),
            new Point3D(1496, 1628, -5),
            DirectionType.East,
            true,
            new Hue(0x83EA),
            MobileFlagsType.None,
            NotorietyType.Innocent
        );

        var bytes = PacketCodec.Encode(packet);

        Assert.Equal(0xFB, bytes[11]);
        Assert.Equal(0x82, bytes[12]);
    }
}
