using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Ultima.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// The two readings the door generator needs from the client files, behind plain delegates.
/// <para>
/// It takes them as functions rather than as a service because Ultima's facets and tiledata are
/// process-wide statics: a test that wanted a doorway would otherwise need a real client directory to
/// prove arithmetic. This is the one place that touches them.
/// </para>
/// </summary>
public static class UltimaStatics
{
    /// <summary>
    /// How far the standable height may sit from the doorway's own. A threshold is a step, not a
    /// storey: further than this and the opening belongs to another floor.
    /// </summary>
    private const int StandableHeightTolerance = 4;

    /// <summary>What the client files call a graphic, or empty when they have nothing to say.</summary>
    public static string Name(int itemId)
        => itemId >= 0 && itemId < TileData.ItemTable.Length ? TileData.ItemTable[itemId].Name ?? "" : "";

    /// <summary>
    /// Whether a body could stand in a tile at roughly that height — the shard's port of the classic
    /// <c>CanFit</c>, which is what tells an opening apart from masonry. The same graphics that frame a
    /// doorway also appear in solid wall, so without this the scan finds an order of magnitude too
    /// many doors.
    /// </summary>
    public static Func<int, int, int, int, bool> Standable(IMapTileService tiles)
        => (mapId, x, y, z) => tiles.TryGetWalkableZ(mapId, x, y, z, [], out var standable) &&
                               Math.Abs(standable - z) <= StandableHeightTolerance;

    /// <summary>Reads the statics on a tile of a facet; empty for a facet this shard does not serve.</summary>
    public static Func<int, int, int, IReadOnlyList<(int Id, int Z)>> Reader(IUltimaMapProvider maps)
        => (mapId, x, y) =>
        {
            if (maps.Get((MapType)mapId) is not { } map || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            {
                return [];
            }

            var statics = map.Tiles.GetStaticTiles(x, y);
            var tiles = new List<(int Id, int Z)>(statics.Length);

            foreach (var tile in statics)
            {
                tiles.Add((tile.Id, tile.Z));
            }

            return tiles;
        };
}
