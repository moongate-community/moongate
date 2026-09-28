using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class MobileIncomingPacketTests
{
    [Fact]
    public void Encode_WritesTheMobileThenEveryItemWithItsHueAndATerminator()
    {
        var packet = new MobileIncomingPacket(
            new Serial(0x00000002), new Body(0x0191), new Point3D(1496, 1628, -5), DirectionType.South, new Hue(0x83EA),
            MobileFlagsType.Female, NotorietyType.Innocent,
            [
                new(new Serial(0x40000001), 0x0E75, LayerType.Backpack, new Hue(0)),
                new(new Serial(0x7EEEEEF0), 0x203C, LayerType.Hair, new Hue(0x044E))
            ]
        );

        var bytes = PacketCodec.Encode(packet);

        Assert.Equal(
            Convert.FromHexString(
                "78" + "0029" + "00000002" + "0191" + "05D8" + "065C" + "FB" + "04" + "83EA" + "02" + "01" +
                "40000001" + "0E75" + "15" + "0000" +
                "7EEEEEF0" + "203C" + "0B" + "044E" +
                "00000000"
            ),
            bytes
        );
    }

    [Fact]
    public void Constructor_TwoItemsOnOneLayer_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MobileIncomingPacket(
                new Serial(2), new Body(0x0191), new Point3D(0, 0, 0), DirectionType.North, new Hue(0), MobileFlagsType.None,
                NotorietyType.Innocent,
                [
                    new(new Serial(0x40000001), 0x1517, LayerType.Shirt, new Hue(0)),
                    new(new Serial(0x40000002), 0x1517, LayerType.Shirt, new Hue(0))
                ]
            )
        );
    }
}
