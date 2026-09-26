using Moongate.Core.DiceNotation;
using Moongate.Core.DiceNotation.Exceptions;

namespace Moongate.Tests.Core.DiceNotation;

public sealed class DiceTests
{
    [Fact]
    public void MinRollAndMaxRoll_ForSimpleDiceExpression_MatchTheBounds()
    {
        var expression = Dice.Parse("2d6+3");

        Assert.Equal(5, expression.MinRoll());
        Assert.Equal(15, expression.MaxRoll());
    }

    [Fact]
    public void MinRollAndMaxRoll_OneDieOfARange_CoverTheRange()
    {
        var expression = Dice.Parse("1d25+95");

        Assert.Equal(96, expression.MinRoll());
        Assert.Equal(120, expression.MaxRoll());
    }

    [Fact]
    public void Parse_ARangeWrittenWithAMinus_IsASubtraction()
    {
        Assert.Equal(-24, Dice.Parse("96-120").Roll());
    }

    [Fact]
    public void Parse_KeepNotAppliedToDice_ThrowsInvalidSyntaxException()
    {
        Assert.Throws<InvalidSyntaxException>(() => Dice.Parse("1k1"));
    }

    [Fact]
    public void Roll_ForSimpleDiceExpression_StaysWithinBounds()
    {
        for (var i = 0; i < 100; i++)
        {
            Assert.InRange(Dice.Roll("2d6+3"), 5, 15);
        }
    }

    [Fact]
    public void Roll_DieWithZeroSides_ThrowsImpossibleDieException()
    {
        Assert.Throws<ImpossibleDieException>(() => Dice.Roll("1d0"));
    }

    [Fact]
    public void Roll_InvalidKeepValue_ThrowsInvalidChooseException()
    {
        Assert.Throws<InvalidChooseException>(() => Dice.Roll("2d6k3"));
    }

    [Fact]
    public void Roll_NegativeMultiplicity_ThrowsInvalidMultiplicityException()
    {
        Assert.Throws<InvalidMultiplicityException>(() => Dice.Roll("-1d6"));
    }
}
