using Moongate.Core.Interfaces.DiceNotation;
using ShaiRandom.Generators;

namespace Moongate.Core.DiceNotation.Terms;

/// <summary>
/// Term representing the subtraction operator -- subtracts the second term from the first.
/// </summary>
public class SubtractTerm : ITerm
{
    /// <summary>
    /// Constructor. Takes the two terms to subtract.
    /// </summary>
    /// <param name="term1">The first term (left-hand side).</param>
    /// <param name="term2">The second term (right-hand side).</param>
    public SubtractTerm(ITerm term1, ITerm term2)
    {
        Term1 = term1;
        Term2 = term2;
    }

    /// <summary>
    /// The first term (left-hand side).
    /// </summary>
    public readonly ITerm Term1;

    /// <summary>
    /// The second term (right-hand side).
    /// </summary>
    public readonly ITerm Term2;

    /// <summary>
    /// Subtracts the second term from the first, evaluating those two terms as necessary.
    /// </summary>
    /// <param name="rng">The rng to used -- passed to other terms.</param>
    /// <returns>The result of evaluating <see cref="Term1" /> - <see cref="Term2" />.</returns>
    public int GetResult(IEnhancedRandom rng)
    {
        return Term1.GetResult(rng) - Term2.GetResult(rng);
    }

    /// <summary>
    /// Returns a parenthesized string representing the operation.
    /// </summary>
    /// <returns>A parenthesized string representing the operation.</returns>
    /// <inheritdoc />
    public (int Min, int Max) GetBounds()
    {
        var (min1, max1) = Term1.GetBounds();
        var (min2, max2) = Term2.GetBounds();

        return (checked(min1 - max2), checked(max1 - min2));
    }

    public override string ToString()
    {
        return $"({Term1}-{Term2})";
    }
}
