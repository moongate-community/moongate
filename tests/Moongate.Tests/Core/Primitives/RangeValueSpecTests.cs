using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public sealed class RangeValueSpecTests
{
    [Fact]
    public void FromValue_Resolve_AlwaysReturnsTheSameValue()
    {
        var spec = RangeValueSpec<int>.FromValue(1150);

        Assert.False(spec.IsRandom);
        Assert.Equal(1150, spec.Resolve());
        Assert.Equal(1150, spec.Resolve());
    }

    [Fact]
    public void FromRange_Resolve_OnlyEverReturnsAValueInTheInclusiveRange()
    {
        var spec = RangeValueSpec<int>.FromRange(1150, 1152);

        for (var i = 0; i < 50; i++)
        {
            var value = spec.Resolve();
            Assert.InRange(value, 1150, 1152);
        }
    }

    [Fact]
    public void FromRange_WithEqualBounds_AlwaysResolvesToThatBound()
    {
        var spec = RangeValueSpec<int>.FromRange(7, 7);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(7, spec.Resolve());
        }
    }

    [Fact]
    public void FromRange_MinGreaterThanMax_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RangeValueSpec<int>.FromRange(10, 5));
    }

    [Theory, InlineData("1150", 1150), InlineData("  1150  ", 1150), InlineData("-5", -5)]
    public void TryParse_ABareNumber_ParsesAFixedValue(string text, int expected)
    {
        Assert.True(RangeValueSpec<int>.TryParse(text, out var spec));
        Assert.False(spec.IsRandom);
        Assert.Equal(expected, spec.Resolve());
    }

    [Fact]
    public void TryParse_ARange_ParsesAFreshPickEachResolve()
    {
        Assert.True(RangeValueSpec<int>.TryParse("1150-1200", out var spec));
        Assert.True(spec.IsRandom);

        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(spec.Resolve(), 1150, 1200);
        }
    }

    [Fact]
    public void TryParse_ARangeWithANegativeLowerBound_ParsesCorrectly()
    {
        Assert.True(RangeValueSpec<int>.TryParse("-10-5", out var spec));
        Assert.True(spec.IsRandom);

        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(spec.Resolve(), -10, 5);
        }
    }

    [Theory, InlineData(null), InlineData(""), InlineData("   "), InlineData("not-a-number"), InlineData("1200-1150")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(RangeValueSpec<int>.TryParse(text, out var spec));
        Assert.False(spec.IsRandom);
    }

    [Fact]
    public void ToString_AFixedValue_WritesTheBareNumber()
    {
        var spec = RangeValueSpec<int>.FromValue(1150);

        Assert.Equal("1150", spec.ToString());
    }

    [Fact]
    public void ToString_ARange_WritesMinDashMax()
    {
        var spec = RangeValueSpec<int>.FromRange(1150, 1200);

        Assert.Equal("1150-1200", spec.ToString());
    }

    [Fact]
    public void ToString_ThenTryParse_RoundTrips()
    {
        var original = RangeValueSpec<int>.FromRange(10, 20);

        Assert.True(RangeValueSpec<int>.TryParse(original.ToString(), out var restored));

        Assert.Equal(original.ToString(), restored.ToString());
    }
}
