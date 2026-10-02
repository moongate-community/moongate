using Moongate.Server.Ultima.Packets.Gumps;

namespace Moongate.Tests.Server.Ultima.Packets.Gumps;

public sealed class GumpResponsePacketTests
{
    [Fact]
    public void TryParse_ReadsTheButtonSwitchesAndTexts()
    {
        var data = Convert.FromHexString(
            "B1002B" + "00000001" + "00000002" + "00000005" +
            "00000002" + "0000000A" + "0000000B" +
            "00000002" + "0003" + "0002" + "00480069" + "0004" + "0000"
        );

        Assert.True(GumpResponsePacket.TryParse(data, out var packet));
        Assert.Equal((1u, 2u, 5), (packet.Serial, packet.TypeId, packet.ButtonId));
        Assert.Equal([10, 11], packet.Switches);
        Assert.Equal([(3, "Hi"), (4, "")], packet.TextEntries);
    }

    [Theory,
     InlineData("B1000F000000010000000200000005"),
     InlineData("B10017000000010000000200000005" + "00000002" + "0000000A"),
     InlineData("B1001B000000010000000200000005" + "00000000" + "00000001" + "00030005")]
    public void TryParse_ATruncatedPacket_Fails(string hex)
    {
        Assert.False(GumpResponsePacket.TryParse(Convert.FromHexString(hex), out _));
    }
}
