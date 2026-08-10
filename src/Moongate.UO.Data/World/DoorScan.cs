using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.World;

/// <summary>
/// Finds the doorways a map's static art draws but leaves empty.
/// <para>
/// Almost every door a player meets is one of these: the bank, the shops, every house. They are not
/// in the decoration corpus — the art draws two frames with a gap between them and nothing in the
/// gap, and a server that wants openable doors has to find the gaps itself. Ported from moongatev2's
/// generator, which is RunUO's <c>[doorgen</c>.
/// </para>
/// <para>
/// Pure, and told about statics through a delegate rather than reading a map: the algorithm is worth
/// testing against a hand-built doorway, and the map files are worth not loading to do it.
/// </para>
/// </summary>
public static class DoorScan
{
    /// <summary>Frames further apart than this in height are on different floors, not one doorway.</summary>
    private const int MaximumFrameHeightDifference = 1;

    /// <summary>
    /// Every doorway in the region, in scan order and at most one door per tile.
    /// </summary>
    /// <param name="mapId">The facet being scanned, carried onto each placement.</param>
    /// <param name="region">Where to look.</param>
    /// <param name="staticsAt">The static graphics and heights at a tile.</param>
    public static IReadOnlyList<DoorPlacement> Scan(
        int mapId,
        DoorScanRegion region,
        Func<int, int, IReadOnlyList<(int Id, int Z)>> staticsAt,
        Func<int, bool>? isDoorwayFrame = null
    )
    {
        isDoorwayFrame ??= _ => true;

        var placements = new List<DoorPlacement>();

        // Two scans of the same wall, or two frames sharing a tile, must not stack doors in one gap.
        var taken = new HashSet<(int X, int Y, int Z)>();

        for (var x = region.StartX; x < region.EndX; x++)
        {
            for (var y = region.StartY; y < region.EndY; y++)
            {
                foreach (var (id, z) in staticsAt(x, y))
                {
                    // A window is a frame too, and hanging a door in one is what the reference
                    // generators do. The caller knows what the graphic is called; this does not.
                    if (!isDoorwayFrame(id))
                    {
                        continue;
                    }

                    if (DoorFrames.IsWest(id))
                    {
                        ScanWestward(mapId, x, y, z, id, staticsAt, taken, placements);
                    }
                    else if (DoorFrames.IsNorth(id))
                    {
                        ScanSouthward(mapId, x, y, z, id, staticsAt, taken, placements);
                    }
                }
            }
        }

        return placements;
    }

    /// <summary>Adds one door unless the gap is already spoken for.</summary>
    private static void Add(
        int mapId,
        int x,
        int y,
        int z,
        DoorFacingType facing,
        int frameId,
        HashSet<(int X, int Y, int Z)> taken,
        List<DoorPlacement> placements
    )
    {
        if (taken.Add((x, y, z)))
        {
            placements.Add(new(mapId, new(x, y, z), facing, frameId));
        }
    }

    /// <summary>The height of the matching frame at a tile, or null when there is none close enough.</summary>
    private static int? MatchingFrameZ(
        int x,
        int y,
        int z,
        Func<int, int, IReadOnlyList<(int Id, int Z)>> staticsAt,
        Func<int, bool> isFrame
    )
    {
        foreach (var (id, frameZ) in staticsAt(x, y))
        {
            if (isFrame(id) && Math.Abs(frameZ - z) <= MaximumFrameHeightDifference)
            {
                return frameZ;
            }
        }

        return null;
    }

    /// <summary>A north frame looking for its south partner: one door at +1, or a pair at +1 and +2.</summary>
    private static void ScanSouthward(
        int mapId,
        int x,
        int y,
        int z,
        int frameId,
        Func<int, int, IReadOnlyList<(int Id, int Z)>> staticsAt,
        HashSet<(int X, int Y, int Z)> taken,
        List<DoorPlacement> placements
    )
    {
        if (MatchingFrameZ(x, y + 2, z, staticsAt, DoorFrames.IsSouth) is { } single)
        {
            Add(mapId, x, y + 1, Math.Min(z, single), DoorFacingType.SouthCW, frameId, taken, placements);

            return;
        }

        if (MatchingFrameZ(x, y + 3, z, staticsAt, DoorFrames.IsSouth) is not { } pair)
        {
            return;
        }

        var floor = Math.Min(z, pair);

        Add(mapId, x, y + 1, floor, DoorFacingType.SouthCW, frameId, taken, placements);
        Add(mapId, x, y + 2, floor, DoorFacingType.NorthCCW, frameId, taken, placements);
    }

    /// <summary>A west frame looking for its east partner: one door at +1, or a pair at +1 and +2.</summary>
    private static void ScanWestward(
        int mapId,
        int x,
        int y,
        int z,
        int frameId,
        Func<int, int, IReadOnlyList<(int Id, int Z)>> staticsAt,
        HashSet<(int X, int Y, int Z)> taken,
        List<DoorPlacement> placements
    )
    {
        if (MatchingFrameZ(x + 2, y, z, staticsAt, DoorFrames.IsEast) is { } single)
        {
            Add(mapId, x + 1, y, Math.Min(z, single), DoorFacingType.WestCW, frameId, taken, placements);

            return;
        }

        if (MatchingFrameZ(x + 3, y, z, staticsAt, DoorFrames.IsEast) is not { } pair)
        {
            return;
        }

        var floor = Math.Min(z, pair);

        Add(mapId, x + 1, y, floor, DoorFacingType.WestCW, frameId, taken, placements);
        Add(mapId, x + 2, y, floor, DoorFacingType.EastCCW, frameId, taken, placements);
    }
}
