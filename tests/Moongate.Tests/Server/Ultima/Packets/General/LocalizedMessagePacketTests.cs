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
}
