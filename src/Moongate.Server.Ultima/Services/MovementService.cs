using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     A port of ModernUO's <c>MovementImpl.CheckMovement</c> and <c>Map.GetAverageZ</c> over terrain and statics,
///     without world items, mobiles, multis, doors or special cases.
/// </summary>
public class MovementService : IMovementService
{
    private const int PersonHeight = 16;
    private const int StepHeight = 2;

    private readonly IMapService _mapService;
    private readonly ITileDataService _tileDataService;

    public MovementService(IMapService mapService, ITileDataService tileDataService)
    {
        _mapService = mapService;
        _tileDataService = tileDataService;
    }

    public int GetAverageZ(MapType map, int x, int y)
    {
        GetAverageZ(map, x, y, out _, out var average, out _);

        return average;
    }

    public bool CheckMovement(
        MapType map,
        Point3D from,
        DirectionType direction,
        MovementAbilityType ability,
        out int newZ
    )
    {
        throw new NotImplementedException();
    }

    private void GetAverageZ(MapType map, int x, int y, out int lowest, out int average, out int highest)
    {
        var zTop = GetLandZ(map, x, y);
        var zLeft = GetLandZ(map, x, y + 1);
        var zRight = GetLandZ(map, x + 1, y);
        var zBottom = GetLandZ(map, x + 1, y + 1);

        lowest = Math.Min(Math.Min(zTop, zLeft), Math.Min(zRight, zBottom));
        highest = Math.Max(Math.Max(zTop, zLeft), Math.Max(zRight, zBottom));
        average = Math.Abs(zTop - zBottom) > Math.Abs(zLeft - zRight)
                      ? FloorAverage(zLeft, zRight)
                      : FloorAverage(zTop, zBottom);
    }

    private int GetLandZ(MapType map, int x, int y)
    {
        if (!_mapService.Maps.Contains(map))
        {
            throw new KeyNotFoundException($"Map {map} is not loaded.");
        }

        return _mapService.Contains(map, x, y) ? _mapService.GetLand(map, x, y).Z : 0;
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
