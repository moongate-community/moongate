using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Primitives;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class WorldItemSaPacketTests
{
    [Fact]
    public void Encode_WritesTwentyFourBytes()
    {
        var packet = new WorldItemSaPacket(
            new Serial(0x40000012),
            0x0EED,
            250,
            new Point3D(1496, 1628, -5),
            new Hue(0x0481),
            false
        );

        Assert.Equal(
            Convert.FromHexString("F3000100400000120EED0000FA00FA05D8065CFB00048100"),
            PacketCodec.Encode(packet)
        );
    }

    [Fact]
    public void Encode_HighSeas_AppendsTwoZeroBytes()
    {
        var packet = new WorldItemSaPacket(new Serial(0x40000012), 0x0EED, 1, new Point3D(1496, 1628, 0), default, true);

        var bytes = PacketCodec.Encode(packet);

        Assert.Equal(26, bytes.Length);
        Assert.Equal([0x00, 0x00], bytes[24..]);
    }
}
