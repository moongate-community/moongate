using Moongate.Core.Interfaces.DiceNotation;
using ShaiRandom.Generators;

namespace Moongate.Core.DiceNotation.Terms;

/// <summary>
/// Term representing the multiplication operator -- multiplies <see cref="Term1" /> and <see cref="Term2" />.
/// </summary>
public class MultiplyTerm : ITerm
{
    /// <summary>
    /// Constructor. Takes the terms that will be multiplied.
    /// </summary>
    /// <param name="term1">The first term (left-hand side).</param>
    /// <param name="term2">The second term (left-hand side).</param>
    public MultiplyTerm(ITerm term1, ITerm term2)
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
    /// Multiplies the first term by the second, evaluating those two terms as necessary.
    /// </summary>
    /// <param name="rng">The rng to used -- passed to other terms.</param>
    /// <returns>The result of evaluating <see cref="Term1" /> * <see cref="Term2" />.</returns>
    public int GetResult(IEnhancedRandom rng)
    {
        return Term1.GetResult(rng) * Term2.GetResult(rng);
    }

    /// <summary>
    /// Returns a parenthesized string representing the term.
    /// </summary>
    /// <returns>A parenthesized string representing the term.</returns>
    /// <inheritdoc />
    public (int Min, int Max) GetBounds()
    {
        var (min1, max1) = Term1.GetBounds();
        var (min2, max2) = Term2.GetBounds();
        int[] products = [checked(min1 * min2), checked(min1 * max2), checked(max1 * min2), checked(max1 * max2)];

        return (products.Min(), products.Max());
    }

    public override string ToString()
    {
        return $"({Term1}*{Term2})";
    }
}
