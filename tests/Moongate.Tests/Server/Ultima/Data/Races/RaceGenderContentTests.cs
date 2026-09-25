using Moongate.Server.Ultima.Data.Races;

namespace Moongate.Tests.Server.Ultima.Data.Races;

public sealed class RaceGenderContentTests
{
    private static readonly RaceGenderContent Male = new()
    {
        Body = 400,
        Hair = [0x203B, 0x203C],
        Beard = [0x203E]
    };

    [Theory, InlineData(0), InlineData(0x203B), InlineData(0x203C)]
    public void IsValidHair_NoHairOrAListedStyle_ReturnsTrue(int style)
    {
        Assert.True(Male.IsValidHair(style));
    }

    [Theory, InlineData(0x2046), InlineData(0x203E), InlineData(0xFFFF)]
    public void IsValidHair_StyleNotListed_ReturnsFalse(int style)
    {
        Assert.False(Male.IsValidHair(style));
    }

    [Theory, InlineData(0, true), InlineData(0x203E, true), InlineData(0x203B, false)]
    public void IsValidBeard_AcceptsNoBeardAndListedStylesOnly(int style, bool expected)
    {
        Assert.Equal(expected, Male.IsValidBeard(style));
    }
}
