using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Tests.Server.Ultima.Packets.General;

public sealed class HuePickerResponsePacketTests
{
    [Fact]
    public void TryParse_ReadsTheIdAndTheHue_AndSkipsTheGraphic()
    {
        // id 7, graphic 0x0FAB, hue 0x0026
        var data = Convert.FromHexString("95000000070FAB0026");

        Assert.True(HuePickerResponsePacket.TryParse(data, out var packet));

        Assert.Equal((7, 0x0026), (packet.PickerId, packet.Hue));
    }

    [Fact]
    public void TryParse_TooShort_Fails()
    {
        Assert.False(HuePickerResponsePacket.TryParse(Convert.FromHexString("9500000007"), out _));
    }
}
