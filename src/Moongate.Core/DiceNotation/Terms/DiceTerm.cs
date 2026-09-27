using Moongate.Core.DiceNotation.Exceptions;
using Moongate.Core.Interfaces.DiceNotation;
using ShaiRandom.Generators;

namespace Moongate.Core.DiceNotation.Terms;

/// <summary>
///     Rolls <see cref="Multiplicity" /> dice of <see cref="Sides" /> sides and sums them. Keeps no state between
///     rolls, so one parsed expression can be rolled from many threads.
/// </summary>
public class DiceTerm : ITerm
{
    /// <summary>
    ///     How many dice to roll.
    /// </summary>
    public ITerm Multiplicity { get; }

    /// <summary>
    ///     How many sides each die has.
    /// </summary>
    public ITerm Sides { get; }

    public DiceTerm(ITerm multiplicity, ITerm sides)
    {
        Multiplicity = multiplicity;
        Sides = sides;
    }

    /// <inheritdoc />
    public int GetResult(IEnhancedRandom rng)
    {
        return RollDice(rng).Sum();
    }

    /// <inheritdoc />
    public (int Min, int Max) GetBounds()
    {
        var (minCount, maxCount) = Multiplicity.GetBounds();
        var (minSides, maxSides) = Sides.GetBounds();

        if (minCount < 0)
        {
            throw new InvalidMultiplicityException();
        }

        if (minSides <= 0)
        {
            throw new ImpossibleDieException();
        }

        return (minCount, checked(maxCount * maxSides));
    }

    /// <summary>
    ///     Rolls every die and returns each result, for <see cref="KeepTerm" /> to pick the highest from.
    /// </summary>
    public List<int> RollDice(IEnhancedRandom rng)
    {
        var count = Multiplicity.GetResult(rng);
        var sides = Sides.GetResult(rng);

        if (count < 0)
        {
            throw new InvalidMultiplicityException();
        }

        if (sides <= 0)
        {
            throw new ImpossibleDieException();
        }

        var results = new List<int>(count);

        for (var i = 0; i < count; i++)
        {
            results.Add(rng.NextInt(1, sides + 1));
        }

        return results;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"({Multiplicity}d{Sides})";
    }
}
