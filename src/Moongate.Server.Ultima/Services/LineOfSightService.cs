using Moongate.Core.Geometry;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Walks POL's integer 3D Bresenham line (<c>realmlos.cpp</c>) and tests each point with ModernUO's rules
///     (<c>Map.LineOfSight</c>), without allocating.
/// </summary>
public class LineOfSightService : ILineOfSightService
{
    private const TileFlagType SightBlockers = TileFlagType.Window | TileFlagType.NoShoot;

    private readonly IMapService _mapService;
    private readonly ITileDataService _tileDataService;
    private readonly LineOfSightConfig _config;

    public LineOfSightService(IMapService mapService, ITileDataService tileDataService, LineOfSightConfig config)
    {
        _mapService = mapService;
        _tileDataService = tileDataService;
        _config = config;
    }

    public bool HasLineOfSight(MapType map, Point3D origin, Point3D target)
    {
        if (!_mapService.Maps.Contains(map))
        {
            throw new KeyNotFoundException($"Map {map} is not loaded.");
        }

        if (origin == target)
        {
            return true;
        }

        if (!_mapService.Contains(map, origin.X, origin.Y) || !_mapService.Contains(map, target.X, target.Y) ||
            Math.Max(Math.Abs(target.X - origin.X), Math.Abs(target.Y - origin.Y)) > _config.MaxDistance)
        {
            return false;
        }

        // Walk from the smaller point by X, then Y, then Z, so A to B and B to A test the same cells.
        var end = target;
        var start = origin;
        var last = target;

        if (start.X > last.X || start.X == last.X && (start.Y > last.Y || start.Y == last.Y && start.Z > last.Z))
        {
            (start, last) = (last, start);
        }

        int x = start.X, y = start.Y, z = start.Z;
        int dx = last.X - x, dy = last.Y - y, dz = last.Z - z;
        int ax = Math.Abs(dx) << 1, ay = Math.Abs(dy) << 1, az = Math.Abs(dz) << 1;
        int sx = Math.Sign(dx), sy = Math.Sign(dy), sz = Math.Sign(dz);
        var cell = new LineOfSightCell { X = -1, Y = -1 };

        if (ax >= ay && ax >= az)
        {
            var yd = ay - (ax >> 1);
            var zd = az - (ax >> 1);

            while (true)
            {
                if (Blocks(map, x, y, z, end, ref cell))
                {
                    return false;
                }

                if (x == last.X)
                {
                    return true;
                }

                if (yd >= 0)
                {
                    y += sy;
                    yd -= ax;
                }

                if (zd >= 0)
                {
                    z += sz;
                    zd -= ax;
                }

                x += sx;
                yd += ay;
                zd += az;
            }
        }

        if (ay >= ax && ay >= az)
        {
            var xd = ax - (ay >> 1);
            var zd = az - (ay >> 1);

            while (true)
            {
                if (Blocks(map, x, y, z, end, ref cell))
                {
                    return false;
                }

                if (y == last.Y)
                {
                    return true;
                }

                if (xd >= 0)
                {
                    x += sx;
                    xd -= ay;
                }

                if (zd >= 0)
                {
                    z += sz;
                    zd -= ay;
                }

                y += sy;
                xd += ax;
                zd += az;
            }
        }

        var xdz = ax - (az >> 1);
        var ydz = ay - (az >> 1);

        while (true)
        {
            if (Blocks(map, x, y, z, end, ref cell))
            {
                return false;
            }

            if (z == last.Z)
            {
                return true;
            }

            if (xdz >= 0)
            {
                x += sx;
                xdz -= az;
            }

            if (ydz >= 0)
            {
                y += sy;
                ydz -= az;
            }

            z += sz;
            xdz += ax;
            ydz += ay;
        }
    }

    // ModernUO's per-point test: land by height, statics with Window or NoShoot, except at the target's cell and height.
    private bool Blocks(MapType map, int x, int y, int z, Point3D end, ref LineOfSightCell cell)
    {
        if (cell.X != x || cell.Y != y)
        {
            LandHeights.Get(_mapService, map, x, y, out var lowest, out _, out var highest);
            cell = new()
            {
                X = x,
                Y = y,
                LandLowest = lowest,
                LandHighest = highest,
                LandIgnored = LandHeights.IsIgnored(_mapService.GetLand(map, x, y).Id),
                Statics = _mapService.GetStatics(map, x, y)
            };
        }

        var top = z + 1;
        var atEnd = x == end.X && y == end.Y;

        if (!cell.LandIgnored && cell.LandLowest <= top && cell.LandHighest >= z &&
            !(atEnd && cell.LandLowest <= end.Z + 1 && cell.LandHighest >= end.Z))
        {
            return true;
        }

        foreach (var tile in cell.Statics)
        {
            var item = _tileDataService.GetItem(tile.Id);

            if ((item.Flags & SightBlockers) == 0)
            {
                continue;
            }

            var tileTop = tile.Z + item.StandHeight;

            if (tile.Z <= top && tileTop >= z && !(atEnd && tile.Z <= end.Z + 1 && tileTop >= end.Z))
            {
                return true;
            }
        }

        return false;
    }
}
