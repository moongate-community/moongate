using Moongate.UO.Data.Hues;

namespace Moongate.Tests.UO.Data.Hues;

public class HueTests
{
    [Fact]
    public void Default_IsZeroAndFlaggedDefault()
    {
        Assert.Equal(0, Hue.Default.Value);
        Assert.True(Hue.Default.IsDefault);
        Assert.False(new Hue(0x1E).IsDefault);
    }

    [Fact]
    public void Equality_AndConversions_RoundTrip()
    {
        var hue = new Hue(0x1E);

        Assert.Equal(new Hue(0x1E), hue);
        Assert.NotEqual(new Hue(0x1F), hue);
        Assert.True(new Hue(0x1E) == hue);
        Assert.False(new Hue(0x1E) != hue);
        Assert.Equal((ushort)0x1E, (ushort)hue);
        Assert.Equal(hue, (Hue)0x1E);
    }
}
