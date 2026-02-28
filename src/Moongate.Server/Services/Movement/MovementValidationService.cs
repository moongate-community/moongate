using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Movement;

/// <summary>
/// Performs server-authoritative movement validation for basic map/tile collisions.
/// </summary>
public sealed class MovementValidationService : IMovementValidationService
{
    private const int PersonHeight = 16;
    private const int StepHeight = 2;
    private const int FallbackStepHeight = 16;

    private readonly IMovementTileQueryService _tileQueryService;
    private readonly ISpatialWorldService _spatialWorldService;

    public MovementValidationService(
        IMovementTileQueryService tileQueryService,
        ISpatialWorldService spatialWorldService
    )
    {
        _tileQueryService = tileQueryService;
        _spatialWorldService = spatialWorldService;
    }

    public bool TryResolveMove(UOMobileEntity mobile, DirectionType direction, out Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        var currentLocation = mobile.Location;
        var destination = currentLocation.Move(direction);
        newLocation = currentLocation;

        if (!_tileQueryService.TryGetMapBounds(mobile.MapId, out var width, out var height))
        {
            newLocation = destination;

            return true;
        }

        if (!IsInsideMap(width, height, destination.X, destination.Y))
        {
            return false;
        }

        var baseDirection = Point3D.GetBaseDirection(direction);

        if (IsDiagonal(baseDirection) && !CanMoveDiagonal(mobile, currentLocation, destination))
        {
            return false;
        }

        if (!TryResolveDestinationZ(mobile, currentLocation, destination, out var resolvedZ))
        {
            return false;
        }

        if (IsBlockedByStatics(mobile.MapId, destination, resolvedZ))
        {
            return false;
        }

        if (IsBlockedByMobiles(mobile, destination, resolvedZ))
        {
            return false;
        }

        newLocation = new(destination.X, destination.Y, resolvedZ);

        return true;
    }

    private static bool IsInsideMap(int width, int height, int x, int y)
        => x >= 0 && y >= 0 && x < width && y < height;

    private bool CanMoveDiagonal(UOMobileEntity mobile, Point3D currentLocation, Point3D destination)
    {
        var sideA = new Point3D(destination.X, currentLocation.Y, currentLocation.Z);
        var sideB = new Point3D(currentLocation.X, destination.Y, currentLocation.Z);

        return IsTileWalkable(mobile.MapId, currentLocation.Z, sideA) &&
               IsTileWalkable(mobile.MapId, currentLocation.Z, sideB);
    }

    private bool TryResolveDestinationZ(UOMobileEntity mobile, Point3D currentLocation, Point3D destination, out int resolvedZ)
    {
        resolvedZ = currentLocation.Z;
        var startZ = currentLocation.Z;
        var supports = CollectSupports(mobile.MapId, destination.X, destination.Y);

        if (supports.Count == 0)
        {
            return false;
        }

        var strictCandidate = SelectBestSupport(supports, startZ, StepHeight);
        var fallbackCandidate = SelectBestSupport(supports, startZ, FallbackStepHeight);
        var upwardCandidate = SelectUpwardSupport(supports, startZ, FallbackStepHeight);

        if (upwardCandidate.HasValue)
        {
            resolvedZ = upwardCandidate.Value;

            return true;
        }

        if (strictCandidate.HasValue && fallbackCandidate.HasValue && fallbackCandidate.Value > strictCandidate.Value)
        {
            resolvedZ = fallbackCandidate.Value;

            return true;
        }

        if (strictCandidate.HasValue)
        {
            resolvedZ = strictCandidate.Value;

            return true;
        }

        if (fallbackCandidate.HasValue)
        {
            resolvedZ = fallbackCandidate.Value;

            return true;
        }

        return false;
    }

    private bool IsTileWalkable(int mapId, int startZ, Point3D location)
    {
        var supports = CollectSupports(mapId, location.X, location.Y);

        if (supports.Count == 0)
        {
            return false;
        }

        return SelectBestSupport(supports, startZ, FallbackStepHeight).HasValue;
    }

    private List<int> CollectSupports(int mapId, int x, int y)
    {
        var supports = new List<int>(8);

        if (_tileQueryService.TryGetLandTile(mapId, x, y, out var landTile))
        {
            var landFlags = GetLandFlags(landTile.ID);

            if (!landTile.Ignored && (landFlags & UOTileFlag.Impassable) == 0)
            {
                supports.Add(landTile.Z);
            }
        }

        foreach (var staticTile in _tileQueryService.GetStaticTiles(mapId, x, y))
        {
            var itemData = GetItemData(staticTile.ID);

            var isStair = itemData[UOTileFlag.StairBack] || itemData[UOTileFlag.StairRight];

            if (!itemData.Surface && !itemData.Bridge && !isStair)
            {
                continue;
            }

            var supportZ = staticTile.Z + itemData.CalcHeight;
            supports.Add(supportZ);
        }

        return supports;
    }

    private static int? SelectBestSupport(IReadOnlyList<int> supports, int startZ, int stepHeight)
    {
        int? best = null;
        var bestDiff = int.MaxValue;

        for (var i = 0; i < supports.Count; i++)
        {
            var candidate = supports[i];

            if (candidate > startZ + stepHeight || candidate < startZ - PersonHeight)
            {
                continue;
            }

            var diff = Math.Abs(candidate - startZ);

            if (diff < bestDiff || diff == bestDiff && (!best.HasValue || candidate > best.Value))
            {
                best = candidate;
                bestDiff = diff;
            }
        }

        return best;
    }

    private static int? SelectUpwardSupport(IReadOnlyList<int> supports, int startZ, int stepHeight)
    {
        int? best = null;

        for (var i = 0; i < supports.Count; i++)
        {
            var candidate = supports[i];

            if (candidate <= startZ || candidate > startZ + stepHeight)
            {
                continue;
            }

            if (!best.HasValue || candidate < best.Value)
            {
                best = candidate;
            }
        }

        return best;
    }

    private bool IsBlockedByStatics(int mapId, Point3D destination, int z)
    {
        var ourTop = z + PersonHeight;

        foreach (var staticTile in _tileQueryService.GetStaticTiles(mapId, destination.X, destination.Y))
        {
            var itemData = GetItemData(staticTile.ID);
            var isStair = itemData[UOTileFlag.StairBack] || itemData[UOTileFlag.StairRight];
            var isWalkableSupport = (itemData.Surface || itemData.Bridge || isStair) && !itemData.Impassable && !itemData.Wall;

            if (isWalkableSupport)
            {
                continue;
            }

            if (!itemData.Impassable && !itemData.ImpassableSurface && !itemData.Wall)
            {
                continue;
            }

            var checkZ = staticTile.Z;
            var checkTop = checkZ + Math.Max(1, itemData.CalcHeight);

            if (checkTop > z && ourTop > checkZ)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBlockedByMobiles(UOMobileEntity mobile, Point3D destination, int resolvedZ)
    {
        var nearbyMobiles = _spatialWorldService.GetNearbyMobiles(destination, 1, mobile.MapId);
        var ourTop = resolvedZ + PersonHeight;

        foreach (var other in nearbyMobiles)
        {
            if (other.Id == mobile.Id)
            {
                continue;
            }

            if (other.Location.X != destination.X || other.Location.Y != destination.Y)
            {
                continue;
            }

            var otherTop = other.Location.Z + PersonHeight;

            if (otherTop > resolvedZ && ourTop > other.Location.Z)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDiagonal(DirectionType direction)
        => direction is DirectionType.NorthEast or DirectionType.SouthEast or DirectionType.SouthWest or DirectionType.NorthWest;

    private static UOTileFlag GetLandFlags(int landId)
    {
        var normalizedLandId = NormalizeTileId(landId, TileData.LandTable.Length);

        return TileData.LandTable[normalizedLandId].Flags;
    }

    private static ItemData GetItemData(int staticId)
    {
        var normalizedStaticId = NormalizeTileId(staticId, TileData.ItemTable.Length);

        return TileData.ItemTable[normalizedStaticId];
    }

    private static int NormalizeTileId(int tileId, int tableLength)
    {
        if (tableLength <= 0)
        {
            return 0;
        }

        if (tileId >= 0 && tileId < tableLength)
        {
            return tileId;
        }

        return tileId & (tableLength - 1);
    }
}
