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
}
