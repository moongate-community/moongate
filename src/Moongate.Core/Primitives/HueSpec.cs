using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Moongate.Core.Random;

namespace Moongate.Core.Primitives;

/// <summary>
///     A hue in a template: either a fixed hue or a range to pick a fresh hue from each time <see cref="Resolve" /> is
///     called, such as when a template spawns an item.
/// </summary>
/// <remarks>
///     Every hue is a 16-bit value (0 to 0xFFFF), written in decimal (<c>1150</c>) or in hex (<c>0x047E</c>). A range
///     is <c>min-max</c> (<c>1150-1200</c>, <c>0x047E-0x04B0</c>) or <c>hue(min:max)</c>; both bounds are inclusive.
/// </remarks>
public readonly struct HueSpec : IEquatable<HueSpec>
{
    private const int MaxHue = 0xFFFF;

    public int Min { get; }

    public int Max { get; }

    /// <summary>
    ///     Gets whether this resolves to a fresh pick from a range rather than always the same hue.
    /// </summary>
    public bool IsRange { get; }

    private HueSpec(int min, int max, bool isRange)
    {
        Min = min;
        Max = max;
        IsRange = isRange;
    }

    /// <summary>
    ///     Creates a spec that always resolves to <paramref name="hue" />.
    /// </summary>
    public static HueSpec FromValue(int hue)
    {
        ThrowIfNotAHue(hue, nameof(hue));

        return new(hue, hue, false);
    }

    /// <summary>
    ///     Creates a spec that resolves to a fresh pick in <c>[min, max]</c> on every call.
    /// </summary>
    public static HueSpec FromRange(int min, int max)
    {
        ThrowIfNotAHue(min, nameof(min));
        ThrowIfNotAHue(max, nameof(max));

        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "min cannot be greater than max.");
        }

        return new(min, max, true);
    }

    /// <summary>
    ///     Parses the text a template writer would use; see the remarks of <see cref="HueSpec" /> for the formats.
    /// </summary>
    /// <exception cref="FormatException">
    ///     The text is not a hue or a hue range.
    /// </exception>
    public static HueSpec Parse(string text)
    {
        return TryParse(text, out var spec)
            ? spec
            : throw new FormatException($"'{text}' is not a hue or a hue range.");
    }

    /// <summary>
    ///     Parses the text a template writer would use; see the remarks of <see cref="HueSpec" /> for the formats.
    /// </summary>
    /// <returns>
    ///     False, with <paramref name="spec" /> left default, when the text is not valid.
    /// </returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out HueSpec spec)
    {
        spec = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        string[] bounds;

        if (trimmed.StartsWith("hue(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            bounds = trimmed[4..^1].Split(':');

            if (bounds.Length != 2)
            {
                return false;
            }
        }
        else
        {
            bounds = trimmed.Split('-');
        }

        switch (bounds.Length)
        {
            case 1 when TryParseHue(bounds[0], out var hue):
                spec = FromValue(hue);

                return true;
            case 2 when TryParseHue(bounds[0], out var min) && TryParseHue(bounds[1], out var max) && min <= max:
                spec = FromRange(min, max);

                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Resolves the hue: the fixed hue, or a fresh pick in the range.
    /// </summary>
    public Hue Resolve()
    {
        return new((ushort)(IsRange ? BuiltInRng.Next(Min, Max - Min + 1) : Min));
    }

    public bool Equals(HueSpec other)
    {
        return Min == other.Min && Max == other.Max && IsRange == other.IsRange;
    }

    public override bool Equals(object? obj)
    {
        return obj is HueSpec other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Min, Max, IsRange);
    }

    /// <summary>
    ///     Writes the hue in hex, such as <c>0x047E</c>, or a range as <c>0x047E-0x04B0</c>; <see cref="Parse" /> reads
    ///     both back.
    /// </summary>
    public override string ToString()
    {
        return IsRange
            ? string.Create(CultureInfo.InvariantCulture, $"0x{Min:X4}-0x{Max:X4}")
            : string.Create(CultureInfo.InvariantCulture, $"0x{Min:X4}");
    }

    public static bool operator ==(HueSpec left, HueSpec right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HueSpec left, HueSpec right)
    {
        return !left.Equals(right);
    }

    private static bool TryParseHue(string text, out int hue)
    {
        var trimmed = text.Trim();
        var parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(trimmed.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hue)
            : int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out hue);

        return parsed && hue is >= 0 and <= MaxHue;
    }

    private static void ThrowIfNotAHue(int hue, string parameterName)
    {
        if (hue is < 0 or > MaxHue)
        {
            throw new ArgumentOutOfRangeException(parameterName, hue, "A hue must be between 0 and 0xFFFF.");
        }
    }
}
