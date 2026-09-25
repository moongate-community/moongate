using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public sealed class HueTests
{
    [Fact]
    public void None_IsZeroAndNotPartial()
    {
        Assert.Equal(0, Hue.None.Value);
        Assert.True(Hue.None.IsNone);
        Assert.False(Hue.None.IsPartial);
    }

    [Theory, InlineData(0x83EA, true), InlineData(0x03EA, false), InlineData(0x8000, true)]
    public void IsPartial_FollowsTheHighBit(int value, bool expected)
    {
        Assert.Equal(expected, new Hue((ushort)value).IsPartial);
    }

    [Fact]
    public void Equality_ComparesTheValue()
    {
        Assert.Equal(new Hue(0x044E), new Hue(0x044E));
        Assert.True(new Hue(0x044E) == new Hue(0x044E));
        Assert.True(new Hue(0x044E) != new Hue(0x044F));
        Assert.Equal(new Hue(0x044E).GetHashCode(), new Hue(0x044E).GetHashCode());
    }

    [Fact]
    public void ToString_IsHexWithFourDigits()
    {
        Assert.Equal("0x83EA", new Hue(0x83EA).ToString());
        Assert.Equal("0x0000", Hue.None.ToString());
    }
}
