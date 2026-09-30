using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Primitives;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class WorldItemPacketTests
{
    [Fact]
    public void Encode_WithoutHue_WritesSixteenBytes()
    {
        var packet = new WorldItemPacket(new Serial(0x40000012), 0x0EED, 250, new Point3D(1496, 1628, -5), default);

        // Serial carries 0x80000000 because the amount follows.
        Assert.Equal(Convert.FromHexString("1A0010C00000120EED00FA05D8065CFB"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Encode_WithHue_SetsTheYBitAndAppendsIt()
    {
        var packet = new WorldItemPacket(new Serial(0x40000012), 0x0EED, 1, new Point3D(1496, 1628, 0), new Hue(0x0481));

        Assert.Equal(Convert.FromHexString("1A0012C00000120EED000105D8865C000481"), PacketCodec.Encode(packet));
    }

    [Fact]
    public void Encode_ALight_FlagsTheXAndWritesItsByteAfterTheY()
    {
        var packet = new WorldItemPacket(new Serial(0x40000012), 0x0EED, 1, new Point3D(1496, 1628, 0), default, 2);

        Assert.Equal(Convert.FromHexString("1A0011C00000120EED000185D8065C0200"), PacketCodec.Encode(packet));
    }
}
