using System.Globalization;
using System.Text.RegularExpressions;

namespace Moongate.UO.Data.Mobiles.Templates;

/// <summary>Parses a hue spec into a concrete hue: a plain integer, a random <c>hue(a:b)</c> range, or 0.</summary>
public static partial class HueSpec
{
    public static ushort Resolve(string? spec, Random random)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return 0;
        }

        var match = RangeRegex().Match(spec);

        if (match.Success)
        {
            var low = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var high = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

            if (high < low)
            {
                (low, high) = (high, low);
            }

            return (ushort)random.Next(low, high + 1);
        }

        return ushort.TryParse(spec, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                   ? value
                   : (ushort)0;
    }

    [GeneratedRegex(@"^hue\(\s*(\d+)\s*:\s*(\d+)\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RangeRegex();
}
