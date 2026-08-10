namespace Moongate.UO.Data.World;

/// <summary>
/// Where to look for doorways, per facet. Ported from moongatev2's generator, which took them from
/// ModernUO's distribution regions.
/// <para>
/// Felucca and Trammel are searched in sixteen rectangles apiece — the towns and dungeons, not the
/// open country between them. Ilshenar and Malas are searched whole, because their built-up areas are
/// scattered enough that bounding them buys little. Together that is <b>21.4 million tiles</b>, which
/// is why the scan does not run on the game loop.
/// </para>
/// <para>
/// Tokuno and Ter Mur are absent, exactly as in the reference. Their doors are not generated, and
/// nobody upstream has written the regions for them.
/// </para>
/// </summary>
public static class DoorScanRegions
{
    /// <summary>The built-up parts of the shared Britannia landmass, which both mirror facets have.</summary>
    private static readonly DoorScanRegion[] Britannia =
    [
        new(250, 750, 775, 1330),
        new(525, 2095, 925, 2430),
        new(1025, 2155, 1265, 2310),
        new(1635, 2430, 1705, 2508),
        new(1775, 2605, 2165, 2975),
        new(1055, 3520, 1570, 4075),
        new(2860, 3310, 3120, 3630),
        new(2470, 1855, 3950, 3045),
        new(3425, 990, 3900, 1455),
        new(4175, 735, 4840, 1600),
        new(2375, 330, 3100, 1045),
        new(2100, 1090, 2310, 1450),
        new(1495, 1400, 1550, 1475),
        new(1085, 1520, 1415, 1910),
        new(1410, 1500, 1745, 1795),
        new(5120, 2300, 6143, 4095)
    ];

    private static readonly Dictionary<int, DoorScanRegion[]> Regions = new()
    {
        [0] = Britannia,
        [1] = Britannia,
        [2] = [new(0, 0, 288 * 8, 200 * 8)],
        [3] = [new(0, 0, 320 * 8, 256 * 8)]
    };

    /// <summary>The facets that have regions at all, ascending.</summary>
    public static IReadOnlyList<int> Maps => [.. Regions.Keys.Order()];

    /// <summary>Where to look on a facet; empty for one nobody has mapped.</summary>
    public static IReadOnlyList<DoorScanRegion> For(int mapId)
        => Regions.GetValueOrDefault(mapId, []);
}
