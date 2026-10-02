using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class UnicodeSpeechRequestPacketTests
{
    [Fact]
    public void TryDecode_UnicodeSay_ReadsSpeechFields()
    {
        var frame = Convert.FromHexString("AD00180003B20003454E550000480065006C006C006F0000");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out var packet));
        Assert.Equal("Hello", packet.Speech.Text);
        Assert.Equal(SpeechType.Regular, packet.Speech.Type);
        Assert.Equal(new Hue(0x03B2), packet.Speech.Hue);
        Assert.Equal(SpeechFontType.Normal, packet.Speech.Font);
        Assert.Equal("ENU", packet.Speech.Language);
    }

    [Fact]
    public void TryDecode_EncodedSay_ReadsUtf8AfterKeywordHeader()
    {
        var frame = Convert.FromHexString("AD00148003B20003454E55000000636166C3A900");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out var packet));
        Assert.Equal("café", packet.Speech.Text);
        Assert.Equal(SpeechType.Regular, packet.Speech.Type);
    }

    [Fact]
    public void TryDecode_EncodedFlags_AreNotSpeechType()
    {
        var frame = Convert.FromHexString("AD0014C003B20003454E55000000636166C3A900");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out var packet));
        Assert.Equal(SpeechType.Regular, packet.Speech.Type);
    }

    [Theory]
    [InlineData("AD00148003B20003454E550000100162616E6B00", new[] { 0x001 })]
    [InlineData("AD00168003B20003454E5500002001002062616E6B00", new[] { 0x001, 0x002 })]
    [InlineData("AD00178003B20003454E550000300200203462616E6B00", new[] { 0x002, 0x002, 0x034 })]
    public void TryDecode_EncodedSayWithKeywordIds_ReadsTheIdsAndTheTextAfterThem(string hex, int[] keywords)
    {
        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(Convert.FromHexString(hex), out var packet));
        Assert.Equal("bank", packet.Speech.Text);
        Assert.Equal(SpeechType.Regular, packet.Speech.Type);
        Assert.Equal(keywords, packet.Speech.Keywords);
    }

    [Fact]
    public void TryDecode_SpacePaddedLanguage_ReadsThreeLetterCode()
    {
        var frame = Convert.FromHexString("AD00180003B20003454E552000480065006C006C006F0000");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out var packet));
        Assert.Equal("ENU", packet.Speech.Language);
    }

    [Fact]
    public void TryDecode_TruncatedKeywordIds_ReturnsFalse()
    {
        var frame = Convert.FromHexString("AD000E8003B20003454E55000010");

        Assert.False(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out _));
    }

    [Fact]
    public void TryDecode_MissingTerminator_ReadsTheTextToTheEnd()
    {
        // As the other emulators: a client that leaves the terminator out is still heard, not disconnected.
        var unicode = Convert.FromHexString("AD00160003B20003454E550000480065006C006C006F");
        var encoded = Convert.FromHexString("AD00138003B20003454E55000000636166C3A9");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(unicode, out var first));
        Assert.Equal("Hello", first.Speech.Text);
        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(encoded, out var second));
        Assert.Equal("café", second.Speech.Text);
    }

    [Fact]
    public void TryDecode_TextAfterTheTerminator_IsDropped()
    {
        var frame = Convert.FromHexString("AD00180003B20003454E5500004800690000005800580000");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out var packet));
        Assert.Equal("Hi", packet.Speech.Text);
    }

    [Fact]
    public void TryDecode_InvalidEncoding_ReplacesTheBadCharacters()
    {
        var invalidSurrogate = Convert.FromHexString("AD00120003B20003454E5500D80000410000");
        var invalidUtf8 = Convert.FromHexString("AD00118003B20003454E55000000FF4100");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(invalidSurrogate, out var first));
        Assert.Equal("\uFFFDA", first.Speech.Text);
        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(invalidUtf8, out var second));
        Assert.Equal("\uFFFDA", second.Speech.Text);
    }

    [Theory,
     InlineData("656E7500", "ENU"),
     InlineData("69746100", "ITA"),
     InlineData("00000000", "ENU"),
     InlineData("31323300", "ENU"),
     InlineData("454E0000", "ENU")]
    public void TryDecode_AnyLanguageBytes_GiveAThreeLetterCode(string languageHex, string expected)
    {
        // A client may send the code in lower case or leave it empty; only three letters are a language.
        var frame = Convert.FromHexString("AD00180003B20003" + languageHex + "00480065006C006C006F0000");

        Assert.True(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(frame, out var packet));
        Assert.Equal(expected, packet.Speech.Language);
    }
}
