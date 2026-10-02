using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Decorations;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     A port of ModernUO's <c>DoorGenerator</c> over the map's terrain and statics: the same regions, door frames and
///     doorways left open, and the items on the ground count as in ModernUO's <c>Map.CanFit</c>: a doorway a
///     decoration file walled up, or one that already has its door, gets none.
/// </summary>
public sealed class DoorGeneratorService : IDoorGeneratorService
{
    private const int ChunkSize = 128;
    private const int DoorHeight = 16;

    private static readonly Rectangle2D[] BritanniaRegions =
    [
        new(new(250, 750), new(775, 1330)), new(new(525, 2095), new(925, 2430)), new(new(1025, 2155), new(1265, 2310)),
        new(new(1635, 2430), new(1705, 2508)), new(new(1775, 2605), new(2165, 2975)), new(new(1055, 3520), new(1570, 4075)),
        new(new(2860, 3310), new(3120, 3630)), new(new(2470, 1855), new(3950, 3045)), new(new(3425, 990), new(3900, 1455)),
        new(new(4175, 735), new(4840, 1600)), new(new(2375, 330), new(3100, 1045)), new(new(2100, 1090), new(2310, 1450)),
        new(new(1495, 1400), new(1550, 1475)), new(new(1085, 1520), new(1415, 1910)), new(new(1410, 1500), new(1745, 1795)),
        new(new(5120, 2300), new(6143, 4095))
    ];

    private static readonly Rectangle2D[] IlshenarRegions = [new(0, 0, 288 * 8, 200 * 8)];

    private static readonly Rectangle2D[] MalasRegions = [new(0, 0, 320 * 8, 256 * 8)];

    private readonly IMapService _maps;
    private readonly ITileDataService _tiles;
    private readonly ISectorService _sectors;

    public DoorGeneratorService(IMapService maps, ITileDataService tiles, ISectorService sectors)
    {
        _sectors = sectors;
        _maps = maps;
        _tiles = tiles;
    }

    public IReadOnlyList<Rectangle2D> ChunksOf(MapType map)
    {
        var regions = map switch
        {
            MapType.Trammel or MapType.Felucca => BritanniaRegions,
            MapType.Ilshenar                   => IlshenarRegions,
            MapType.Malas                      => MalasRegions,
            _                                  => []
        };
        var chunks = new List<Rectangle2D>();

        if (!_maps.Maps.Contains(map))
        {
            return chunks;
        }

        foreach (var region in regions)
        {
            for (var x = region.X; x < region.X + region.Width; x += ChunkSize)
            {
                for (var y = region.Y; y < region.Y + region.Height; y += ChunkSize)
                {
                    var width = Math.Min(ChunkSize, region.X + region.Width - x);
                    var height = Math.Min(ChunkSize, region.Y + region.Height - y);

                    // The map may be smaller than ModernUO's regions.
                    while (width > 0 && !_maps.Contains(map, x + width - 1, y))
                    {
                        width--;
                    }

                    while (width > 0 && height > 0 && !_maps.Contains(map, x, y + height - 1))
                    {
                        height--;
                    }

                    if (width > 0 && height > 0)
                    {
                        chunks.Add(new(x, y, width, height));
                    }
                }
            }
        }

        return chunks;
    }

    public IReadOnlyList<GeneratedDoor> Scan(MapType map, Rectangle2D chunk)
    {
        var doors = new List<GeneratedDoor>();

        for (var x = chunk.X; x < chunk.X + chunk.Width; x++)
        {
            for (var y = chunk.Y; y < chunk.Y + chunk.Height; y++)
            {
                if (!_maps.Contains(map, x, y))
                {
                    continue;
                }

                foreach (var tile in _maps.GetStatics(map, x, y))
                {
                    if (DoorFrames.IsWest(tile.Id))
                    {
                        if (HasFrame(map, x + 2, y, tile.Z, true, out var z))
                        {
                            Add(doors, map, new(x + 1, y, Math.Min(tile.Z, z)), DoorFacingType.WestCW);
                        }
                        else if (HasFrame(map, x + 3, y, tile.Z, true, out z))
                        {
                            AddPair(
                                doors,
                                map,
                                new(new(x + 1, y, Math.Min(tile.Z, z)), DoorFacingType.WestCW),
                                new(new(x + 2, y, Math.Min(tile.Z, z)), DoorFacingType.EastCCW)
                            );
                        }
                    }
                    else if (DoorFrames.IsNorth(tile.Id))
                    {
                        if (HasFrame(map, x, y + 2, tile.Z, false, out var z))
                        {
                            Add(doors, map, new(x, y + 1, Math.Min(tile.Z, z)), DoorFacingType.SouthCW);
                        }
                        else if (HasFrame(map, x, y + 3, tile.Z, false, out z))
                        {
                            AddPair(
                                doors,
                                map,
                                new(new(x, y + 1, Math.Min(tile.Z, z)), DoorFacingType.NorthCCW),
                                new(new(x, y + 2, Math.Min(tile.Z, z)), DoorFacingType.SouthCW)
                            );
                        }
                    }
                }
            }
        }

        return doors;
    }

    // An east (or south) frame on the cell within a step of the first frame's height.
    private bool HasFrame(MapType map, int x, int y, int z, bool east, out int frameZ)
    {
        frameZ = 0;

        if (!_maps.Contains(map, x, y))
        {
            return false;
        }

        foreach (var tile in _maps.GetStatics(map, x, y))
        {
            if ((east ? DoorFrames.IsEast(tile.Id) : DoorFrames.IsSouth(tile.Id)) && Math.Abs(tile.Z - z) <= 1)
            {
                frameZ = tile.Z;

                return true;
            }
        }

        return false;
    }

    private void Add(List<GeneratedDoor> doors, MapType map, Point3D location, DoorFacingType facing)
    {
        if (Fits(map, location))
        {
            doors.Add(new(location, facing));
        }
    }

    // ModernUO deletes the half that fits when the other does not.
    private void AddPair(List<GeneratedDoor> doors, MapType map, GeneratedDoor first, GeneratedDoor second)
    {
        if (Fits(map, first.Location) && Fits(map, second.Location))
        {
            doors.Add(first);
            doors.Add(second);
        }
    }

    private bool Fits(MapType map, Point3D location)
    {
        var (x, y, z) = (location.X, location.Y, location.Z);

        // The doorways ModernUO leaves open, the Ilshenar ruins among them; its last test has no map, as there.
        if ((y == 1743 && x is >= 1343 and <= 1344) ||
            (y == 1679 && x is >= 1392 and <= 1393) ||
            (x == 1320 && y is >= 1618 and <= 1640) ||
            (x == 1383 && y is >= 1642 and <= 1643) ||
            (map == MapType.Ilshenar && x is >= 644 and <= 670 && y is >= 925 and <= 941) ||
            (x == 985 && y == 994))
        {
            return false;
        }

        return CanFit(map, x, y, z);
    }

    // ModernUO Map.CanFit(x, y, z, 16, false, false): nothing solid in the door's space, static or item, and a surface
    // right under it.
    private bool CanFit(MapType map, int x, int y, int z)
    {
        if (!_maps.Contains(map, x, y))
        {
            return false;
        }

        var hasSurface = false;
        var land = _maps.GetLand(map, x, y);
        LandHeights.Get(_maps, map, x, y, out var lowest, out var average, out _);
        var landImpassable = (_tiles.GetLand(land.Id & 0x3FFF).Flags & TileFlagType.Impassable) != 0;

        if (landImpassable && average > z && z + DoorHeight > lowest)
        {
            return false;
        }

        if (!landImpassable && z == average && !LandHeights.IsIgnored(land.Id))
        {
            hasSurface = true;
        }

        foreach (var tile in _maps.GetStatics(map, x, y))
        {
            if (Blocks(tile.Id, tile.Z, z, ref hasSurface))
            {
                return false;
            }
        }

        foreach (var item in _sectors.GetItemsInRange(map, new(x, y, z), 0))
        {
            if (item.GroundLocation is { } spot && Blocks(item.ItemId, spot.Z, z, ref hasSurface))
            {
                return false;
            }
        }

        return hasSurface;
    }

    // Whether a static or an item with this graphic at tileZ stands in the door's space; one it can stand on sets
    // hasSurface. A graphic the tile data does not know is ignored.
    private bool Blocks(int graphic, int tileZ, int z, ref bool hasSurface)
    {
        if (!_tiles.TryGetItem(graphic, out var tile))
        {
            return false;
        }

        var surface = (tile.Flags & TileFlagType.Surface) != 0;
        var impassable = (tile.Flags & TileFlagType.Impassable) != 0;

        if ((surface || impassable) && tileZ + tile.StandHeight > z && z + DoorHeight > tileZ)
        {
            return true;
        }

        if (surface && !impassable && z == tileZ + tile.StandHeight)
        {
            hasSurface = true;
        }

        return false;
    }
}
