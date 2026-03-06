using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory storage for door component metadata and precomputed toggle definitions.
/// </summary>
public class DoorDataService : IDoorDataService
{
    // ModernUO BaseDoor offsets indexed by DoorFacing enum (0..7).
    private static readonly Point3D[] OffsetsByDoorFacing =
    [
        new(-1, 1, 0), // WestCW
        new(1, 1, 0),  // EastCCW
        new(-1, 0, 0), // WestCCW
        new(1, -1, 0), // EastCW
        new(1, 1, 0),  // SouthCW
        new(1, -1, 0), // NorthCCW
        new(0, 0, 0),  // SouthCCW
        new(0, -1, 0)  // NorthCW
    ];

    // doors.txt piece order: WestCCW, EastCW, WestCW, EastCCW, SouthCW, NorthCCW, SouthCCW, NorthCW
    private static readonly int[] PieceIndexToDoorFacing =
    [
        2,
        3,
        0,
        1,
        4,
        5,
        6,
        7
    ];

    private readonly object _sync = new();
    private List<DoorComponentEntry> _entries = [];
    private Dictionary<int, DoorToggleDefinition> _toggleByItemId = [];

    public IReadOnlyList<DoorComponentEntry> GetAllEntries()
    {
        lock (_sync)
        {
            return [.. _entries];
        }
    }

    public void SetEntries(IReadOnlyList<DoorComponentEntry> entries)
    {
        lock (_sync)
        {
            _entries = [.. entries];
            _toggleByItemId = BuildToggleMap(entries);
        }
    }

    public bool TryGetToggleDefinition(int itemId, out DoorToggleDefinition definition)
    {
        lock (_sync)
        {
            return _toggleByItemId.TryGetValue(itemId, out definition);
        }
    }

    private static Dictionary<int, DoorToggleDefinition> BuildToggleMap(IReadOnlyList<DoorComponentEntry> entries)
    {
        var map = new Dictionary<int, DoorToggleDefinition>();

        foreach (var entry in entries)
        {
            Span<int> pieces =
            [
                entry.Piece1,
                entry.Piece2,
                entry.Piece3,
                entry.Piece4,
                entry.Piece5,
                entry.Piece6,
                entry.Piece7,
                entry.Piece8
            ];

            for (var pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
            {
                var closedId = pieces[pieceIndex];

                if (closedId <= 0)
                {
                    continue;
                }

                var openedId = checked(closedId + 1);
                var doorFacingIndex = PieceIndexToDoorFacing[pieceIndex];
                var offset = OffsetsByDoorFacing[doorFacingIndex];

                map[closedId] = new(closedId, openedId, true, offset);
                map[openedId] = new(openedId, closedId, false, offset);

                // Legacy recovery for old wrong-id spawns (closedId - 1).
                var legacyClosedId = closedId - 1;

                if (legacyClosedId > 0 && !map.ContainsKey(legacyClosedId))
                {
                    map[legacyClosedId] = new(legacyClosedId, openedId, true, offset);
                }
            }
        }

        return map;
    }
}
