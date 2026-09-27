using ShaiRandom.Generators;

namespace Moongate.Core.Interfaces.DiceNotation;

/// <summary>
/// Interface for a term of a dice expression that can be evaluated.
/// </summary>
public interface ITerm
{
    /// <summary>
    /// Evaluates the term and returns the result.
    /// </summary>
    /// <param name="rng">The rng to use.</param>
    /// <returns>The result of evaluating the term.</returns>
    int GetResult(IEnhancedRandom rng);

    /// <summary>
    ///     Gets the smallest and the largest result the term can give, checking at parse time what a roll would
    ///     reject, such as a die with no sides or a division by zero.
    /// </summary>
    (int Min, int Max) GetBounds();
}
