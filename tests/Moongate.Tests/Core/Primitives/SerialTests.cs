using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public class SerialTests
{
    [Theory]
    [InlineData(0x00000001u, true, false)]
    [InlineData(0x3FFFFFFFu, true, false)]
    [InlineData(0x40000000u, false, true)]
    [InlineData(0x7FFFFFFFu, false, true)]
    [InlineData(0x00000000u, false, false)]
    [InlineData(0x80000000u, false, false)]
    public void IsMobile_IsItem_FollowProtocolRanges(uint value, bool isMobile, bool isItem)
    {
        var serial = new Serial(value);

        Assert.Equal(isMobile, serial.IsMobile);
        Assert.Equal(isItem, serial.IsItem);
    }

    [Fact]
    public void IsValid_ZeroIsInvalid()
    {
        Assert.False(Serial.Zero.IsValid);
        Assert.True(new Serial(1).IsValid);
    }

    [Fact]
    public void Equality_And_Ordering_FollowValue()
    {
        var low = new Serial(5);
        var high = new Serial(9);

        Assert.True(low == new Serial(5));
        Assert.True(low != high);
        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= new Serial(5));
        var sameAsHigh = new Serial(9);
        Assert.True(high >= sameAsHigh);
        Assert.True(low.CompareTo(high) < 0);
    }

    [Fact]
    public void Conversions_RoundTripThroughUint()
    {
        var serial = (Serial)0x40000123u;
        uint back = serial;

        Assert.Equal(0x40000123u, back);
        Assert.Equal(0x40000123u, serial.Value);
    }

    [Fact]
    public void ToString_IsHexWith8Digits()
    {
        Assert.Equal("0x4000000A", new Serial(0x4000000Au).ToString());
        Assert.Equal("0x00000001", new Serial(1).ToString());
    }
}
