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

    [Fact]
    public void IsValid_ZeroIsInvalid()
    {
        Assert.False(Serial.Zero.IsValid);
        Assert.True(new Serial(1).IsValid);
    }

    [Theory, InlineData(0x7EEEEEEFu, true), InlineData(0x7FFFFFFFu, true),

     // The band is exclusive: the last item serial is not virtual, and neither is a mobile.
     InlineData(0x7EEEEEEEu, false), InlineData(0x40000000u, false), InlineData(0x00000001u, false)]
    public void IsVirtual_CoversOnlyTheReservedBand(uint value, bool isVirtual)
        => Assert.Equal(isVirtual, new Serial(value).IsVirtual);

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

    // The pair that matters: whatever ToString wrote, TryParse must read back. Everything the API
    // hands out is in that form, so this round trip is what lets a caller feed a serial back to us.
    [Theory]
    [InlineData(0x4000000Au)]
    [InlineData(1u)]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void TryParse_ReadsBackWhatToStringWrote(uint value)
    {
        var serial = new Serial(value);

        Assert.True(Serial.TryParse(serial.ToString(), out var parsed));
        Assert.Equal(serial, parsed);
    }

    [Theory]
    [InlineData("0x40000001", 0x40000001u)]
    [InlineData("0X40000001", 0x40000001u)]
    [InlineData("40000001", 40000001u)]
    [InlineData("57005", 57005u)]
    public void TryParse_TakesHexWithThePrefixAndPlainDecimalWithout(string text, uint expected)
    {
        Assert.True(Serial.TryParse(text, out var parsed));
        Assert.Equal(new Serial(expected), parsed);
    }

    // Without the prefix a bare "40000001" is decimal, so the prefix is what picks the base -- not a
    // guess at whether the digits look hexadecimal.
    [Fact]
    public void TryParse_WithoutThePrefix_DoesNotGuessHex()
    {
        Assert.False(Serial.TryParse("4000000A", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("banana")]
    [InlineData("0x")]
    [InlineData("-1")]
    [InlineData("0x100000000")]
    [InlineData("4294967296")]
    public void TryParse_RejectsWhatIsNotASerial(string text)
    {
        Assert.False(Serial.TryParse(text, out var parsed));
        Assert.Equal(Serial.Zero, parsed);
    }

    [Fact]
    public void TryParse_RejectsNull()
        => Assert.False(Serial.TryParse(null, out _));

    [Fact]
    public void VirtualBand_StartsRightAfterTheLastItem()
        => Assert.Equal(Serial.MaxItem + 1, Serial.MinVirtual);
}
