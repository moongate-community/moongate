using Moongate.Core.Interfaces.DiceNotation;
using ShaiRandom.Generators;

namespace Moongate.Core.DiceNotation.Terms;

/// <summary>
/// Term representing the division operator -- divides the first term by the second.
/// </summary>
public class DivideTerm : ITerm
{
    /// <summary>
    /// Constructor. Takes the two terms to divide.
    /// </summary>
    /// <param name="term1">The first term (left-hand side).</param>
    /// <param name="term2">The second term (right-hand side).</param>
    public DivideTerm(ITerm term1, ITerm term2)
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
    /// Divides the first term by the second, evaluating those two terms as necessary.
    /// </summary>
    /// <param name="rng">The rng to used -- passed to other terms.</param>
    /// <returns>The result of evaluating <see cref="Term1" /> / <see cref="Term2" />.</returns>
    public int GetResult(IEnhancedRandom rng)
    {
        return Divide(Term1.GetResult(rng), Term2.GetResult(rng));
    }

    /// <summary>
    /// Returns a parenthesized string representing the operation.
    /// </summary>
    /// <returns>A parenthesized string representing the operation.</returns>
    /// <inheritdoc />
    /// <exception cref="DivideByZeroException">The divisor can be 0.</exception>
    public (int Min, int Max) GetBounds()
    {
        var (min1, max1) = Term1.GetBounds();
        var (min2, max2) = Term2.GetBounds();

        if (min2 <= 0 && max2 >= 0)
        {
            throw new DivideByZeroException($"The divisor {Term2} can be 0.");
        }

        // Rounding is monotonic, so the extremes are at the corners of both ranges.
        int[] quotients =
        [
            Divide(min1, min2), Divide(min1, max2), Divide(max1, min2), Divide(max1, max2)
        ];

        return (quotients.Min(), quotients.Max());
    }

    public override string ToString()
    {
        return $"({Term1}/{Term2})";
    }

    private static int Divide(int dividend, int divisor)
    {
        return (int)Math.Round((double)dividend / divisor);
    }
}
