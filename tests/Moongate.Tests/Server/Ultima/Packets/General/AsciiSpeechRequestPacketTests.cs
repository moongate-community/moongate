using Moongate.Core.Primitives;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class AsciiSpeechRequestPacketTests
{
    [Fact]
    public void TryDecode_SayFrame_ReadsSpeechFields()
    {
        var frame = Convert.FromHexString("03000E0003B2000348656C6C6F00");

        Assert.True(PacketCodec.TryDecode<AsciiSpeechRequestPacket>(frame, out var packet));
        Assert.Equal("Hello", packet.Speech.Text);
        Assert.Equal(SpeechType.Regular, packet.Speech.Type);
        Assert.Equal(new Hue(0x03B2), packet.Speech.Hue);
        Assert.Equal(SpeechFontType.Normal, packet.Speech.Font);
        Assert.Equal("ENU", packet.Speech.Language);
    }

    [Theory]
    [InlineData("03000DC003B2000362616E6B00", SpeechType.Regular)]
    [InlineData("03000DC803B2000362616E6B00", SpeechType.Whisper)]
    public void TryDecode_KeywordFlaggedAsciiFrame_ReadsUnderlyingSpeechType(string hex, SpeechType expectedType)
    {
        Assert.True(PacketCodec.TryDecode<AsciiSpeechRequestPacket>(Convert.FromHexString(hex), out var packet));
        Assert.Equal(expectedType, packet.Speech.Type);
        Assert.Equal("bank", packet.Speech.Text);
    }

    [Fact]
    public void TryDecode_MalformedFrame_ReturnsFalse()
    {
        var valid = Convert.FromHexString("03000E0003B2000348656C6C6F00");
        var wrongLength = valid.ToArray();
        wrongLength[2] = 0x0F;

        Assert.False(PacketCodec.TryDecode<AsciiSpeechRequestPacket>(wrongLength, out _));
        Assert.False(PacketCodec.TryDecode<AsciiSpeechRequestPacket>(valid.AsSpan(0, valid.Length - 1), out _));
        Assert.False(PacketCodec.TryDecode<AsciiSpeechRequestPacket>([.. valid[..^1], 0x21], out _));
    }
}
