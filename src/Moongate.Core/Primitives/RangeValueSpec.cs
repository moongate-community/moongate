using System.Globalization;
using System.Numerics;
using Moongate.Core.Random;

namespace Moongate.Core.Primitives;

/// <summary>
///     A numeric field that is either a fixed value or a range to pick a fresh value from each time
///     <see cref="Resolve" /> is called, such as when a template spawns an entity.
/// </summary>
/// <remarks>
///     Written as text: a bare number (
///     <c>
///         1150
///     </c>
///     ) is fixed;
///     <c>
///         1150-1200
///     </c>
///     picks a fresh integer in
///     that inclusive range on every <see cref="Resolve" />. A range with equal bounds is legal and always
///     resolves to that bound.
/// </remarks>
public readonly struct RangeValueSpec<T> where T : struct, INumber<T>
{
    private readonly T _fixedValue;
    private readonly T _min;
    private readonly T _max;

    /// <summary>
    ///     Gets whether this resolves to a fresh pick from a range rather than always the same value.
    /// </summary>
    public bool IsRandom { get; }

    /// <summary>
    ///     Gets the smallest value <see cref="Resolve" /> can return: the fixed value, or the bottom of the range.
    /// </summary>
    public T Min => IsRandom ? _min : _fixedValue;

    private RangeValueSpec(T fixedValue)
    {
        _fixedValue = fixedValue;
        _min = fixedValue;
        _max = fixedValue;
        IsRandom = false;
    }

    private RangeValueSpec(T min, T max)
    {
        _fixedValue = default;
        _min = min;
        _max = max;
        IsRandom = true;
    }

    /// <summary>
    ///     Creates a spec that always resolves to <paramref name="value" />.
    /// </summary>
    public static RangeValueSpec<T> FromValue(T value)
    {
        return new(value);
    }

    /// <summary>
    ///     Creates a spec that resolves to a fresh pick in
    ///     <c>
    ///         [min, max]
    ///     </c>
    ///     on every call.
    /// </summary>
    public static RangeValueSpec<T> FromRange(T min, T max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "min cannot be greater than max.");
        }

        return new(min, max);
    }

    /// <summary>
    ///     Parses the text a template writer would use: a bare number, or
    ///     <c>
    ///         min-max
    ///     </c>
    ///     .
    /// </summary>
    /// <returns>
    ///     False, with <paramref name="spec" /> left default, when the text is not valid.
    /// </returns>
    public static bool TryParse(string? text, out RangeValueSpec<T> spec)
    {
        spec = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        // A leading '-' is a sign, not the range separator, so the search for it starts at index 1.
        var separator = trimmed.IndexOf('-', 1);

        if (separator > 0)
        {
            var minText = trimmed[..separator];
            var maxText = trimmed[(separator + 1)..];

            if (!T.TryParse(minText, CultureInfo.InvariantCulture, out var min) ||
                !T.TryParse(maxText, CultureInfo.InvariantCulture, out var max) ||
                min > max)
            {
                return false;
            }

            spec = FromRange(min, max);

            return true;
        }

        if (!T.TryParse(trimmed, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        spec = FromValue(value);

        return true;
    }

    /// <summary>
    ///     Resolves the value: the fixed value, or a fresh pick in the range.
    /// </summary>
    public T Resolve()
    {
        if (!IsRandom)
        {
            return _fixedValue;
        }

        var span = int.CreateChecked(_max - _min) + 1;

        return _min + T.CreateChecked(BuiltInRng.Next(span));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsRandom
            ? $"{_min.ToString(null, CultureInfo.InvariantCulture)}-{_max.ToString(null, CultureInfo.InvariantCulture)}"
            : _fixedValue.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
