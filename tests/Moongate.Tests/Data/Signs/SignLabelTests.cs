using Moongate.UO.Data.Signs;

namespace Moongate.Tests.Data.Signs;

/// <summary>
/// A sign says what it says in one of two ways, and the shipped corpus uses both: 450 of the 509
/// signs name a cliloc, the other 59 carry the words themselves.
/// </summary>
public class SignLabelTests
{
    [Fact]
    public void AClilocReference_BecomesItsNumber()
    {
        var (cliloc, text) = SignLabel.Split("#1016093");

        Assert.Equal(1016093, cliloc);
        Assert.Equal("", text);
    }

    [Fact]
    public void LiteralText_StaysText()
    {
        var (cliloc, text) = SignLabel.Split("The Shakin' Bakery");

        Assert.Equal(0, cliloc);
        Assert.Equal("The Shakin' Bakery", text);
    }

    // A label is data from a hand-edited file. A broken one costs that sign its name, not the run.
    [Theory, InlineData(""), InlineData("   "), InlineData(null), InlineData("#"), InlineData("#abc"),
     InlineData("#-5")]
    public void AMalformedLabel_YieldsNeither(string? label)
    {
        var (cliloc, text) = SignLabel.Split(label);

        Assert.Equal(0, cliloc);
        Assert.Equal("", text);
    }

    [Fact]
    public void SurroundingWhitespace_IsNotPartOfTheName()
        => Assert.Equal("Britain Bank", SignLabel.Split("  Britain Bank  ").Text);

    // A hash inside the words is a hash, not a reference: only a leading one names a cliloc.
    [Fact]
    public void AHashThatDoesNotStartTheLabel_IsJustText()
        => Assert.Equal("Bob's #1 Tavern", SignLabel.Split("Bob's #1 Tavern").Text);
}
