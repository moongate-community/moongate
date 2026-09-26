using Moongate.Core.DiceNotation.Exceptions;
using Moongate.Core.Interfaces.DiceNotation;
using ShaiRandom.Generators;

namespace Moongate.Core.DiceNotation.Terms;

/// <summary>
///     Rolls a <see cref="DiceTerm" /> and keeps the highest <see cref="Keep" /> dice, such as <c>4d6k3</c>.
/// </summary>
public class KeepTerm : ITerm
{
    /// <summary>
    ///     The dice to roll.
    /// </summary>
    public DiceTerm DiceTerm { get; }

    /// <summary>
    ///     How many of the highest dice to keep.
    /// </summary>
    public ITerm Keep { get; }

    public KeepTerm(ITerm keep, DiceTerm diceTerm)
    {
        DiceTerm = diceTerm;
        Keep = keep;
    }

    /// <inheritdoc />
    public int GetResult(IEnhancedRandom rng)
    {
        var keep = Keep.GetResult(rng);

        if (keep < 0)
        {
            throw new InvalidChooseException();
        }

        var results = DiceTerm.RollDice(rng);

        if (keep > results.Count)
        {
            throw new InvalidChooseException();
        }

        return results.OrderByDescending(value => value).Take(keep).Sum();
    }

    /// <inheritdoc />
    public (int Min, int Max) GetBounds()
    {
        var (minKeep, maxKeep) = Keep.GetBounds();
        var (minCount, _) = DiceTerm.Multiplicity.GetBounds();
        var (_, maxSides) = DiceTerm.Sides.GetBounds();
        DiceTerm.GetBounds();

        if (minKeep < 0 || maxKeep > minCount)
        {
            throw new InvalidChooseException();
        }

        return (minKeep, checked(maxKeep * maxSides));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"({DiceTerm}k{Keep})";
    }
}
