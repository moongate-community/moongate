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
    public void TryDecode_MissingTerminatorOrInvalidEncoding_ReturnsFalse()
    {
        var missingTerminator = Convert.FromHexString("AD00160003B20003454E550000480065006C006C006F");
        var invalidSurrogate = Convert.FromHexString("AD00100003B20003454E5500D8000000");
        var invalidUtf8 = Convert.FromHexString("AD00108003B20003454E55000000FF00");

        Assert.False(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(missingTerminator, out _));
        Assert.False(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(invalidSurrogate, out _));
        Assert.False(PacketCodec.TryDecode<UnicodeSpeechRequestPacket>(invalidUtf8, out _));
    }
}
