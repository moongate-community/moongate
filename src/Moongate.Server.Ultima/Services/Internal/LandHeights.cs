using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The terrain heights of a cell from its four corners, as ModernUO's <c>Map.GetAverageZ</c>, shared by movement and
///     line of sight.
/// </summary>
internal static class LandHeights
{
    /// <summary>
    ///     Reads the land Z of (x, y), (x, y+1), (x+1, y) and (x+1, y+1); a corner outside the map counts as 0.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     <paramref name="map" /> was not loaded.
    /// </exception>
    public static void Get(
        IMapService mapService,
        MapType map,
        int x,
        int y,
        out int lowest,
        out int average,
        out int highest
    )
    {
        if (!mapService.Maps.Contains(map))
        {
            throw new KeyNotFoundException($"Map {map} is not loaded.");
        }

        var zTop = GetLandZ(mapService, map, x, y);
        var zLeft = GetLandZ(mapService, map, x, y + 1);
        var zRight = GetLandZ(mapService, map, x + 1, y);
        var zBottom = GetLandZ(mapService, map, x + 1, y + 1);

        lowest = Math.Min(Math.Min(zTop, zLeft), Math.Min(zRight, zBottom));
        highest = Math.Max(Math.Max(zTop, zLeft), Math.Max(zRight, zBottom));
        average = Math.Abs(zTop - zBottom) > Math.Abs(zLeft - zRight)
                      ? FloorAverage(zLeft, zRight)
                      : FloorAverage(zTop, zBottom);
    }

    /// <summary>
    ///     Gets whether the client leaves this land tile undrawn, such as the black void under caves; only the statics on
    ///     it count.
    /// </summary>
    public static bool IsIgnored(ushort landId)
    {
        return landId is 2 or 0x1DB or >= 0x1AE and <= 0x1B5;
    }

    private static int GetLandZ(IMapService mapService, MapType map, int x, int y)
    {
        return mapService.Contains(map, x, y) ? mapService.GetLand(map, x, y).Z : 0;
    }

    private static int FloorAverage(int a, int b)
    {
        var sum = a + b;

        if (sum < 0)
        {
            --sum;
        }

        return sum / 2;
    }
}
