using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public class SerialTests
{
    [Fact]
    public void Conversions_RoundTripThroughUint()
    {
        var serial = (Serial)0x40000123u;
        uint back = serial;

        Assert.Equal(0x40000123u, back);
        Assert.Equal(0x40000123u, serial.Value);
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
    public void GetHashCode_EqualValues_AreEqual()
        => Assert.Equal(new Serial(0x40000123u).GetHashCode(), new Serial(0x40000123u).GetHashCode());

    [Theory, InlineData(0x00000001u, true, false), InlineData(0x3FFFFFFFu, true, false),
     InlineData(0x40000000u, false, true), InlineData(0x7EEEEEEEu, false, true), InlineData(0x00000000u, false, false),
     InlineData(0x80000000u, false, false),
     // The virtual band sits above the items and belongs to neither.
     InlineData(0x7EEEEEEFu, false, false), InlineData(0x7FFFFFFFu, false, false)]
    public void IsMobile_IsItem_FollowProtocolRanges(uint value, bool isMobile, bool isItem)
    {
        var serial = new Serial(value);

        Assert.Equal(isMobile, serial.IsMobile);
        Assert.Equal(isItem, serial.IsItem);
    }

    [Theory, InlineData(0x7EEEEEEFu, true), InlineData(0x7FFFFFFFu, true),
     // The band is exclusive: the last item serial is not virtual, and neither is a mobile.
     InlineData(0x7EEEEEEEu, false), InlineData(0x40000000u, false), InlineData(0x00000001u, false)]
    public void IsVirtual_CoversOnlyTheReservedBand(uint value, bool isVirtual)
        => Assert.Equal(isVirtual, new Serial(value).IsVirtual);

    [Fact]
    public void VirtualBand_StartsRightAfterTheLastItem()
        => Assert.Equal(Serial.MaxItem + 1, Serial.MinVirtual);

    [Fact]
    public void IsValid_ZeroIsInvalid()
    {
        Assert.False(Serial.Zero.IsValid);
        Assert.True(new Serial(1).IsValid);
    }

    [Fact]
    public void ProtocolConstants_MatchWireRanges()
    {
        Assert.Equal(0x00000001u, Serial.MinMobile);
        Assert.Equal(0x3FFFFFFFu, Serial.MaxMobile);
        Assert.Equal(0x40000000u, Serial.MinItem);
        Assert.Equal(0x7EEEEEEEu, Serial.MaxItem);
        Assert.Equal(0x7EEEEEEFu, Serial.MinVirtual);
        Assert.Equal(0x7FFFFFFFu, Serial.MaxVirtual);
    }

    [Fact]
    public void ToString_IsHexWith8Digits()
    {
        Assert.Equal("0x4000000A", new Serial(0x4000000Au).ToString());
        Assert.Equal("0x00000001", new Serial(1).ToString());
    }
}
