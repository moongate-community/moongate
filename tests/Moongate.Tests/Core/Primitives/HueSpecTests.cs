using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public sealed class HueSpecTests
{
    [Fact]
    public void FromValue_Resolve_AlwaysReturnsTheSameHue()
    {
        var spec = HueSpec.FromValue(1150);

        Assert.False(spec.IsRange);
        Assert.Equal(new Hue(1150), spec.Resolve());
        Assert.Equal(new Hue(1150), spec.Resolve());
    }

    [Fact]
    public void FromRange_Resolve_OnlyEverReturnsAHueInTheInclusiveRange()
    {
        var spec = HueSpec.FromRange(1150, 1152);
        var seen = new HashSet<int>();

        for (var i = 0; i < 200; i++)
        {
            var hue = spec.Resolve().Value;
            Assert.InRange(hue, 1150, 1152);
            seen.Add(hue);
        }

        Assert.Equal(3, seen.Count);
    }

    [Theory, InlineData(-1), InlineData(0x10000)]
    public void FromValue_OutsideTheHueRange_Throws(int hue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HueSpec.FromValue(hue));
    }

    [Fact]
    public void FromRange_MinAboveMax_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HueSpec.FromRange(1200, 1150));
    }

    [Theory,
     InlineData("1150", 1150, 1150, false),
     InlineData("0x047E", 0x047E, 0x047E, false),
     InlineData(" 0X047e ", 0x047E, 0x047E, false),
     InlineData("0", 0, 0, false),
     InlineData("65535", 0xFFFF, 0xFFFF, false),
     InlineData("1150-1200", 1150, 1200, true),
     InlineData("0x047E-0x04B0", 0x047E, 0x04B0, true),
     InlineData("hue(1150:1200)", 1150, 1200, true),
     InlineData("HUE(0x10:0x20)", 0x10, 0x20, true),
     InlineData("1150-1150", 1150, 1150, true)]
    public void Parse_ValidText_ReadsTheHueOrRange(string text, int min, int max, bool isRange)
    {
        var spec = HueSpec.Parse(text);

        Assert.Equal(min, spec.Min);
        Assert.Equal(max, spec.Max);
        Assert.Equal(isRange, spec.IsRange);
    }

    [Theory,
     InlineData(null),
     InlineData(""),
     InlineData("   "),
     InlineData("red"),
     InlineData("-5"),
     InlineData("65536"),
     InlineData("0x10000"),
     InlineData("1200-1150"),
     InlineData("1-2-3"),
     InlineData("hue(1150)"),
     InlineData("hue(1:2:3)"),
     InlineData("0x"),
     InlineData("1150-")]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(HueSpec.TryParse(text, out var spec));
        Assert.Equal(default, spec);
    }

    [Fact]
    public void Parse_InvalidText_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => HueSpec.Parse("red"));
    }

    [Theory, InlineData("1150", "0x047E"), InlineData("1150-1200", "0x047E-0x04B0")]
    public void ToString_WritesHexThatParsesBackUnchanged(string text, string expected)
    {
        var spec = HueSpec.Parse(text);

        Assert.Equal(expected, spec.ToString());
        Assert.Equal(spec, HueSpec.Parse(spec.ToString()));
    }

    [Fact]
    public void Equality_ComparesBoundsAndKind()
    {
        Assert.Equal(HueSpec.Parse("1150"), HueSpec.Parse("0x047E"));
        Assert.True(HueSpec.FromRange(1, 2) == HueSpec.Parse("hue(1:2)"));
        Assert.NotEqual(HueSpec.FromValue(5), HueSpec.FromRange(5, 5));
    }
}
