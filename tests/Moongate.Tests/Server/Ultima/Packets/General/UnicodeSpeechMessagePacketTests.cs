using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class UnicodeSpeechMessagePacketTests
{
    [Fact]
    public void Encode_PlayerSpeech_WritesProtocolFieldsAndBigEndianText()
    {
        var packet = new UnicodeSpeechMessagePacket(
            new Serial(0x12345678),
            0x0190,
            SpeechType.Regular,
            new Hue(0x03B2),
            SpeechFontType.Normal,
            "ENU",
            "Alice",
            "Hello"
        );

        var actual = PacketCodec.Encode(packet);
        var expected = Convert.FromHexString(
            "AE003C1234567801900003B20003454E5500" +
            "416C696365" + new string('0', 50) +
            "00480065006C006C006F0000"
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Encode_SystemSpeech_HasPrivateSystemIdentity()
    {
        var packet = new UnicodeSpeechMessagePacket(
            new Serial(uint.MaxValue),
            ushort.MaxValue,
            SpeechType.System,
            new Hue(0x03B2),
            SpeechFontType.Normal,
            "ENU",
            "System",
            "OK"
        );

        var actual = PacketCodec.Encode(packet);

        Assert.Equal(54, actual.Length);
        Assert.Equal(Convert.FromHexString("AE0036FFFFFFFFFFFF0103B20003454E5500"), actual.AsSpan(0, 18).ToArray());
        Assert.Equal(Convert.FromHexString("004F004B0000"), actual.AsSpan(48).ToArray());
    }

    [Fact]
    public void Constructor_InvalidField_RejectsUnencodablePacket()
    {
        static UnicodeSpeechMessagePacket Create(string language, string name, string text)
        {
            return new(
                new Serial(1),
                0x0190,
                SpeechType.Regular,
                new Hue(0x03B2),
                SpeechFontType.Normal,
                language,
                name,
                text
            );
        }

        Assert.Throws<ArgumentException>(() => Create("ENGLISH", "Alice", "hello"));
        Assert.Throws<ArgumentException>(() => Create("ENU", new string('A', 31), "hello"));
        Assert.Throws<ArgumentException>(() => Create("ENU", "Alice", new string('A', 33000)));
    }
}
