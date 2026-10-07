using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Tests.Server.Ultima.Packets.World;

public sealed class DisplayPaperdollPacketTests
{
    [Fact]
    public void Encode_WritesSerialTitleAndFlags()
    {
        var bytes = PacketCodec.Encode(new DisplayPaperdollPacket(new(0x00000123), "Aria, the mage", false, true));

        Assert.Equal(66, bytes.Length);
        Assert.Equal(Convert.FromHexString("8800000123"), bytes[..5]);
        Assert.Equal("Aria, the mage"u8.ToArray(), bytes[5..19]);
        Assert.All(bytes[19..65], value => Assert.Equal(0, value));
        Assert.Equal(0x02, bytes[65]);
    }

    [Fact]
    public void Encode_WarMode_SetsTheFirstFlag()
    {
        var bytes = PacketCodec.Encode(new DisplayPaperdollPacket(new(0x00000123), "Aria", true, false));

        Assert.Equal(0x01, bytes[65]);
    }

    [Fact]
    public void Encode_ALongOrNonAsciiTitle_IsCutToSixtyAsciiBytes()
    {
        var bytes = PacketCodec.Encode(
            new DisplayPaperdollPacket(new(0x00000123), "Città" + new string('x', 70), false, false)
        );

        Assert.Equal(66, bytes.Length);
        Assert.Equal("Citt?xxxxx"u8.ToArray(), bytes[5..15]);
        Assert.Equal((byte)'x', bytes[64]);
    }
}
