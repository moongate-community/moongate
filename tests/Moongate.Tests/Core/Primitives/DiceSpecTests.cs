using Moongate.Core.Primitives;

namespace Moongate.Tests.Core.Primitives;

public sealed class DiceSpecTests
{
    [Fact]
    public void FromValue_IsConstant()
    {
        var spec = DiceSpec.FromValue(-2500);

        Assert.True(spec.IsConstant);
        Assert.Equal((-2500, -2500, -2500), (spec.Min, spec.Max, spec.Roll()));
        Assert.Equal("-2500", spec.ToString());
    }

    [Theory, InlineData("-2500", -2500), InlineData(" 42 ", 42)]
    public void Parse_AnIntegerText_IsAConstant(string text, int value)
    {
        var spec = DiceSpec.Parse(text);

        Assert.True(spec.IsConstant);
        Assert.Equal(value, spec.Roll());
    }

    [Fact]
    public void Parse_AnExpression_RollsWithinItsBounds()
    {
        var spec = DiceSpec.Parse("1d25+95");

        Assert.False(spec.IsConstant);
        Assert.Equal((96, 120), (spec.Min, spec.Max));
        Assert.All(Enumerable.Range(0, 200), _ => Assert.InRange(spec.Roll(), 96, 120));
        Assert.Equal("1d25+95", spec.ToString());
    }

    [Fact]
    public void Parse_ARangeWrittenWithAMinus_IsASubtraction()
    {
        Assert.Equal(-24, DiceSpec.Parse("96-120").Roll());
    }

    [Theory,
     InlineData("3d4+2", 5, 14),
     InlineData("4d6k3", 3, 18),
     InlineData("(2d6+1)*10", 30, 130)]
    public void Parse_TheDocumentedForms_HaveTheirBounds(string text, int min, int max)
    {
        var spec = DiceSpec.Parse(text);

        Assert.Equal((min, max), (spec.Min, spec.Max));
    }

    [Theory,
     InlineData("10-1d4", 6, 9),
     InlineData("1d4-1d6", -5, 3),
     InlineData("20/1d4", 5, 20),
     InlineData("(2d6+1)-3", 0, 10),
     InlineData("(1d6)-1", 0, 5),
     InlineData("2d6 + 3", 5, 15),
     InlineData("2 - 3", -1, -1)]
    public void Parse_DiceOnEitherSideOfAnOperator_HaveExactBounds(string text, int min, int max)
    {
        var spec = DiceSpec.Parse(text);

        Assert.Equal((min, max), (spec.Min, spec.Max));
        Assert.All(Enumerable.Range(0, 500), _ => Assert.InRange(spec.Roll(), min, max));
    }

    [Theory, InlineData(""), InlineData("abc"), InlineData("2d"), InlineData(null),
     InlineData("1d6 fire"), InlineData("5 apples"), InlineData("(1d6"), InlineData("1d6)"), InlineData("1d2O"),
     InlineData("5/0"), InlineData("1d6/(1d2-1)"), InlineData("100000*100000")]
    public void TryParse_BadText_ReturnsFalseAndParseThrows(string? text)
    {
        Assert.False(DiceSpec.TryParse(text, out _));
        Assert.Throws<FormatException>(() => DiceSpec.Parse(text!));
    }
}
