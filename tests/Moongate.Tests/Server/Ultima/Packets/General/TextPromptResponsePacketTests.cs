using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class TextPromptResponsePacketTests
{
    [Fact]
    public void TryParse_AnAnswer_ReadsTheIdAndTheText()
    {
        // length 25, serial 2, id 7, type 1, language ENU, "Hi" in little-endian UTF-16 and its terminator
        var data = Convert.FromHexString("C20019" + "00000002" + "00000007" + "00000001" + "454E5500" + "48006900" + "0000");

        Assert.True(TextPromptResponsePacket.TryParse(data, out var packet));

        Assert.Equal((2u, 7, false, "Hi"), (packet.Serial.Value, packet.PromptId, packet.IsCancel, packet.Text));
    }

    [Fact]
    public void TryParse_AnEscape_IsACancel()
    {
        var data = Convert.FromHexString("C20015" + "00000002" + "00000007" + "00000000" + "454E5500" + "0000");

        Assert.True(TextPromptResponsePacket.TryParse(data, out var packet));

        Assert.True(packet.IsCancel);
        Assert.Equal("", packet.Text);
    }

    [Fact]
    public void TryParse_ControlCharacters_AreLeftOut()
    {
        var data = Convert.FromHexString("C20019" + "00000002" + "00000007" + "00000001" + "454E5500" + "48000A00" + "6900");

        Assert.True(TextPromptResponsePacket.TryParse(data, out var packet));

        Assert.Equal("Hi", packet.Text);
    }

    [Fact]
    public void TryParse_AnOddLengthOrNoTerminator_ReadsWhatIsWhole()
    {
        // "Hi" and a stray last byte, no terminator
        var data = Convert.FromHexString("C20018" + "00000002" + "00000007" + "00000001" + "454E5500" + "48006900" + "41");

        Assert.True(TextPromptResponsePacket.TryParse(data, out var packet));

        Assert.Equal("Hi", packet.Text);
    }

    [Fact]
    public void TryParse_TooShort_Fails()
    {
        Assert.False(TextPromptResponsePacket.TryParse(Convert.FromHexString("C2000800000002"), out _));
    }
}
