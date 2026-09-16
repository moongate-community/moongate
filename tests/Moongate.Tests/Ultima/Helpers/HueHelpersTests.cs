using SkiaSharp;
using Moongate.Ultima.Helpers;

namespace Moongate.Tests.Ultima.Helpers;

public class HueHelpersTests
{
    [Theory,
     InlineData(0x7C00, 255, 0, 0),
     InlineData(0x03E0, 0, 255, 0),
     InlineData(0x001F, 0, 0, 255),
     InlineData(0x0000, 0, 0, 0),
     InlineData(0x0443, 8, 16, 24)]
    public void HueToColor_Rgb555_ExpandsChannelsAndReturnsOpaqueColor(ushort hue, byte red, byte green, byte blue)
    {
        var color = HueHelpers.HueToColor(hue);

        Assert.Equal(new SKColor(red, green, blue, 255), color);
        Assert.Equal(hue, HueHelpers.ColorToHue(color));
    }
}
