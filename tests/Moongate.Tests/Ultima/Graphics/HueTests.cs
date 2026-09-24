using Moongate.Ultima.Graphics;

namespace Moongate.Tests.Ultima.Graphics;

public class HueTests
{
    [Fact]
    public void GetColor_PaletteEntry_PreservesLegacyChannelScaling()
    {
        var hue = new Hue(0);
        hue.Colors[2] = 0x7FE1;

        var color = hue.GetColor(2);

        Assert.Equal(new(248, 248, 8, 255), color);
    }
}
