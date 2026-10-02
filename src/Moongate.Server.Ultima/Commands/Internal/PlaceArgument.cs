using System.Globalization;
using Moongate.Core.Geometry;
using Moongate.Core.Utils;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Commands.Internal;

/// <summary>
///     Reads the place a command is given, as <c>go</c> and <c>moongate</c> take it: <c>&lt;x&gt;,&lt;y&gt;,&lt;z&gt;</c>
///     then at most the name of a map. The numbers may be split by commas or by spaces.
/// </summary>
internal static class PlaceArgument
{
    private static readonly char[] Separators = [',', ' '];

    /// <summary>
    ///     Reads three numbers, then at most the name of a map; without one the map is <paramref name="own" />.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> arguments, MapType own, out MapType map, out Point3D location)
    {
        map = own;
        location = default;
        var parts = string.Join(' ', arguments).Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is < 3 or > 4 ||
            !TryParseNumber(parts[0], 0, ushort.MaxValue, out var x) ||
            !TryParseNumber(parts[1], 0, ushort.MaxValue, out var y) ||
            !TryParseNumber(parts[2], sbyte.MinValue, sbyte.MaxValue, out var z))
        {
            return false;
        }

        location = new Point3D(x, y, z);

        // A map is named, never numbered: a fourth number is a mistake.
        return parts.Length == 3 ||
               !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
               EnumNameUtils.TryParse(parts[3], out map) &&
               Enum.IsDefined(map);
    }

    private static bool TryParseNumber(string text, int minimum, int maximum, out int value)
    {
        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) &&
               value >= minimum &&
               value <= maximum;
    }
}
