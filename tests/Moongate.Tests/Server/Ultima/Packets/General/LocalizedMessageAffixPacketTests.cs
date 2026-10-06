using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class LocalizedMessageAffixPacketTests
{
    [Fact]
    public void Spoken_WritesTheSpeakerTheTextAndWhatIsAppendedToIt()
    {
        var bytes = PacketCodec.Encode(
            LocalizedMessageAffixPacket.Spoken(new Serial(0x100), 0x0190, 1042673, "Bank Teller", "5,000")
        );

        // serial, body, type 0 (regular), hue 0x3B2, font 3, cliloc 1042673, affix type 0 (appended)
        Assert.Equal(
            Convert.FromHexString("CC" + "0039" + "00000100" + "0190" + "00" + "03B2" + "0003" + "000FE8F1" + "00"),
            bytes[..19]
        );
        // The name in thirty bytes, zero filled.
        Assert.Equal("Bank Teller"u8.ToArray(), bytes[19..30]);
        Assert.All(bytes[30..49], value => Assert.Equal(0, value));
        // The affix in ASCII with its zero, then the arguments, here none: their zero of two bytes.
        Assert.Equal("5,000\0"u8.ToArray(), bytes[49..55]);
        Assert.Equal([0, 0], bytes[55..57]);
        Assert.Equal(57, bytes.Length);
    }

    [Fact]
    public void Spoken_WithArguments_WritesThemInBigEndianUtf16()
    {
        var bytes = PacketCodec.Encode(
            LocalizedMessageAffixPacket.Spoken(new Serial(0x100), 0x0190, 1042673, "B", "!", "ab")
        );

        Assert.Equal([(byte)'!', 0, 0, (byte)'a', 0, (byte)'b', 0, 0], bytes[49..57]);
        Assert.Equal(bytes.Length, bytes[1] << 8 | bytes[2]);
    }

    // The affix is ASCII on the wire: what is not becomes a question mark, and never ends the text early.
    [Fact]
    public void Spoken_AnAffixThatIsNotAscii_IsFolded()
    {
        var bytes = PacketCodec.Encode(LocalizedMessageAffixPacket.Spoken(new Serial(0x100), 0x0190, 1042673, "B", "5 000"));

        Assert.Equal("5?000", Encoding.ASCII.GetString(bytes, 49, 5));
    }
}
