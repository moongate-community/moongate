using System.Globalization;
using Moongate.Core.DiceNotation;

namespace Moongate.Core.Primitives;

/// <summary>
///     A numeric field rolled with dice notation, such as
///     <c>
///         "1d25+95"
///     </c>
///     or
///     <c>
///         "3d6+10"
///     </c>
///     , or a constant.
/// </summary>
/// <remarks>
///     A uniform range from a to b is one die,
///     <c>
///         1d(b-a+1)+(a-1)
///     </c>
///     : 96 to 120 is
///     <c>
///         1d25+95
///     </c>
///     .
///     <c>
///         "96-120"
///     </c>
///     is a subtraction, -24, not a range.
/// </remarks>
public readonly struct DiceSpec
{
    private readonly DiceExpression? _expression;
    private readonly string? _text;

    /// <summary>
    ///     Gets whether every roll gives the same value.
    /// </summary>
    public bool IsConstant => _expression is null;

    /// <summary>
    ///     Gets the smallest value <see cref="Roll" /> can return.
    /// </summary>
    public int Min { get; }

    /// <summary>
    ///     Gets the largest value <see cref="Roll" /> can return.
    /// </summary>
    public int Max { get; }

    private DiceSpec(int value)
    {
        _expression = null;
        _text = null;
        Min = value;
        Max = value;
    }

    private DiceSpec(DiceExpression expression, string text)
    {
        _expression = expression;
        _text = text;
        Min = expression.MinRoll();
        Max = expression.MaxRoll();
    }

    /// <summary>
    ///     Creates a spec that always rolls <paramref name="value" />.
    /// </summary>
    public static DiceSpec FromValue(int value)
    {
        return new(value);
    }

    /// <summary>
    ///     Parses an integer, negative allowed, or a dice expression.
    /// </summary>
    /// <exception cref="FormatException">The text is neither.</exception>
    public static DiceSpec Parse(string text)
    {
        return TryParse(text, out var spec)
            ? spec
            : throw new FormatException($"'{text}' is not a number or a dice expression.");
    }

    /// <summary>
    ///     Parses an integer, negative allowed, or a dice expression.
    /// </summary>
    /// <returns>False, with <paramref name="spec" /> left default, when the text is neither.</returns>
    public static bool TryParse(string? text, out DiceSpec spec)
    {
        spec = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            spec = FromValue(value);

            return true;
        }

        try
        {
            spec = new(Dice.Parse(trimmed), trimmed);

            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Rolls the value: the constant, or a fresh roll of the expression.
    /// </summary>
    public int Roll()
    {
        return _expression?.Roll() ?? Min;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _text ?? Min.ToString(CultureInfo.InvariantCulture);
    }
}
