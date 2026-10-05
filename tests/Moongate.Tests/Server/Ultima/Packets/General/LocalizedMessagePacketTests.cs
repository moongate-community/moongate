using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class LocalizedMessagePacketTests
{
    [Fact]
    public void Encode_WritesTheLabelOverTheObject()
    {
        var bytes = PacketCodec.Encode(new LocalizedMessagePacket(new(0x40000010), 0x0EED, 1050039, "", "5\t#1023821"));

        Assert.Equal(50 + "5\t#1023821".Length * 2, bytes.Length);
        Assert.Equal(Convert.FromHexString("C1" + (50 + 20).ToString("X4") + "40000010" + "0EED" + "06" + "03B2" + "0003" + "001005B7"), bytes[..18]);
        Assert.All(bytes[18..48], value => Assert.Equal(0, value));
        Assert.Equal("5\t#1023821"u8.ToArray().SelectMany(b => new byte[] { b, 0 }).ToArray(), bytes[48..68]);
        Assert.Equal(new byte[] { 0, 0 }, bytes[68..70]);
    }

    [Fact]
    public void System_WritesARegularMessageOfNoObject_NamedSystem()
    {
        var bytes = PacketCodec.Encode(LocalizedMessagePacket.System(500867));

        Assert.Equal(50, bytes.Length);
        // serial -1, graphic -1, type 0 (regular), hue 0x3B2, font 3, cliloc 500867
        Assert.Equal(Convert.FromHexString("C1" + "0032" + "FFFFFFFF" + "FFFF" + "00" + "03B2" + "0003" + "0007A483"), bytes[..18]);
        Assert.Equal("System"u8.ToArray(), bytes[18..24]);
    }

    [Fact]
    public void System_WithAHue_WritesThatHue()
    {
        var bytes = PacketCodec.Encode(LocalizedMessagePacket.System(500112, "", 0x3F));

        Assert.Equal(Convert.FromHexString("003F"), bytes[10..12]);
    }

    [Fact]
    public void Spoken_WritesARegularMessageOfTheSpeaker_WithItsBodyAndName()
    {
        var bytes = PacketCodec.Encode(LocalizedMessagePacket.Spoken(new Serial(0x100), 0x0190, 1042759, "Bank Teller", "1,200"));

        // serial, body, type 0 (regular), hue 0x3B2, font 3, cliloc 1042759
        Assert.Equal(Convert.FromHexString("C1" + "003C" + "00000100" + "0190" + "00" + "03B2" + "0003" + "000FE947"), bytes[..18]);
        Assert.Equal("Bank Teller"u8.ToArray(), bytes[18..29]);
        // The arguments in little-endian UTF-16, then their end.
        Assert.Equal(System.Text.Encoding.Unicode.GetBytes("1,200"), bytes[48..58]);
        Assert.Equal([0, 0], bytes[58..60]);
    }
}
