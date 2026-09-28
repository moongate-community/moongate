using System.Text;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.Characters;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class CharacterListUpdatePacketTests
{
    [Fact]
    public void Encode_WritesEverySlotAsNameThenAnEmptyPassword()
    {
        var bytes = PacketCodec.Encode(new CharacterListUpdatePacket(["Aria", null, null, null, null]));

        Assert.Equal(4 + 5 * 60, bytes.Length);
        Assert.Equal(new byte[] { 0x86, 0x01, 0x30, 0x05 }, bytes[..4]);
        Assert.Equal("Aria", Encoding.ASCII.GetString(bytes, 4, 4));
        Assert.All(bytes[8..64], b => Assert.Equal(0, b));
        Assert.All(bytes[64..], b => Assert.Equal(0, b));
    }

    [Theory, InlineData(0), InlineData(2), InlineData(8)]
    public void Constructor_UnsupportedSlotCount_Throws(int count)
    {
        Assert.Throws<ArgumentException>(() => new CharacterListUpdatePacket(new string?[count]));
    }
}
