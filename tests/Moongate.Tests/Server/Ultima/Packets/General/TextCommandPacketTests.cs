using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class TextCommandPacketTests
{
    [Fact]
    public void TryParse_ReadsTheKindAndTheTextUpToItsZero()
    {
        // kind 0x24, "21 0"
        var data = Convert.FromHexString("120009243231203000");

        Assert.True(TextCommandPacket.TryParse(data, out var packet));

        Assert.Equal((TextCommandPacket.UseSkill, "21 0", 9), (packet.Kind, packet.Text, packet.Length));
    }

    [Fact]
    public void TryParse_ATextWithoutItsZero_IsReadWhole()
    {
        Assert.True(TextCommandPacket.TryParse(Convert.FromHexString("1200052431"), out var packet));

        Assert.Equal("1", packet.Text);
    }

    [Fact]
    public void TryParse_AKindWithoutText_HasAnEmptyText()
    {
        Assert.True(TextCommandPacket.TryParse(Convert.FromHexString("12000458"), out var packet));

        Assert.Equal(((byte)0x58, ""), (packet.Kind, packet.Text));
    }

    [Fact]
    public void TryParse_ATextThatIsNotAscii_IsEmpty()
    {
        Assert.True(TextCommandPacket.TryParse(Convert.FromHexString("12000624FF00"), out var packet));

        Assert.Equal((TextCommandPacket.UseSkill, ""), (packet.Kind, packet.Text));
    }

    [Fact]
    public void TryParse_TooShortForAKind_HasNone()
    {
        Assert.True(TextCommandPacket.TryParse(Convert.FromHexString("120003"), out var packet));

        Assert.Equal(((byte)0, ""), (packet.Kind, packet.Text));
    }
}
