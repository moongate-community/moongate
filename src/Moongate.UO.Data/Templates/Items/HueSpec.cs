using Moongate.Core.Random;
using ShaiRandom.Generators;
using System.Globalization;

namespace Moongate.UO.Data.Templates.Items;

/// <summary>
/// Represents a hue specification that can be either a fixed value or a runtime range.
/// </summary>
public readonly record struct HueSpec
{
    private HueSpec(int min, int max, bool isRange)
    {
        Min = min;
        Max = max;
        IsRange = isRange;
    }

    public int Min { get; }

    public int Max { get; }

    public bool IsRange { get; }

    public static HueSpec ParseFromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("HueSpec string cannot be null or empty.");
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("hue(", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(')'))
        {
            var content = trimmed[4..^1];
            var parts = content.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var min) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var max))
            {
                throw new FormatException($"Invalid hue range format: {value}");
            }

            return FromRange(min, max);
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
            {
                throw new FormatException($"Invalid hexadecimal hue value: {value}");
            }

            return FromValue(hexValue);
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue))
        {
            return FromValue(numericValue);
        }

        throw new FormatException($"Invalid hue value: {value}");
    }

    public static HueSpec FromRange(int min, int max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min cannot be greater than max");
        }

        return new(min, max, true);
    }

    public static HueSpec FromValue(int value)
        => new(value, value, false);

    public int Resolve(IEnhancedRandom? rng = null)
    {
        if (!IsRange)
        {
            return Min;
        }

        rng ??= BuiltInRng.Generator;

        return rng.NextInt(Min, Max + 1);
    }

    public override string ToString()
        => IsRange ? $"hue({Min}:{Max})" : $"0x{Min:X4}";
}
