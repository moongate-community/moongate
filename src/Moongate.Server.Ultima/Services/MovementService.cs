using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
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
        LandHeights.Get(_mapService, map, x, y, out _, out var average, out _);

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
        // Only the low three bits name the direction, as ModernUO's Direction.Mask; the rest, running included, is ignored.
        var baseDirection = (DirectionType)((byte)direction & 0x7);
        var forward = from.Move(baseDirection);

        if (!_mapService.Contains(map, from.X, from.Y) || !_mapService.Contains(map, forward.X, forward.Y))
        {
            if (!_mapService.Maps.Contains(map))
            {
                throw new KeyNotFoundException($"Map {map} is not loaded.");
            }

            newZ = from.Z;

            return false;
        }

        GetStartZ(map, from, ability, out var startZ, out var startTop);

        var moveIsOk = Check(map, forward.X, forward.Y, from.Z, startZ, startTop, ability, out newZ);

        // A diagonal step also needs both cells beside it; they lie inside the map because forward does.
        if (moveIsOk && ((byte)baseDirection & 0x1) == 0x1)
        {
            var left = from.Move((DirectionType)(((byte)baseDirection - 1) & 0x7));
            var right = from.Move((DirectionType)(((byte)baseDirection + 1) & 0x7));

            moveIsOk = Check(map, left.X, left.Y, from.Z, startZ, startTop, ability, out _) &&
                       Check(map, right.X, right.Y, from.Z, startZ, startTop, ability, out _);
        }

        if (!moveIsOk)
        {
            newZ = startZ;
        }

        return moveIsOk;
    }

    // ModernUO MovementImpl.GetStartZ: the surface the mover stands on (zLow) and the top of what it stands in (zTop).
    private void GetStartZ(MapType map, Point3D from, MovementAbilityType ability, out int zLow, out int zTop)
    {
        var canSwim = (ability & MovementAbilityType.Swim) != 0;
        var cantWalk = (ability & MovementAbilityType.Walk) == 0;
        var land = _mapService.GetLand(map, from.X, from.Y);
        var landBlocks = LandBlocks(land, canSwim, cantWalk);

        LandHeights.Get(_mapService, map, from.X, from.Y, out var landZ, out var landCenter, out var landTop);

        var considerLand = !LandHeights.IsIgnored(land.Id);
        var zCenter = zLow = zTop = 0;
        var isSet = false;

        if (considerLand && !landBlocks && from.Z >= landCenter)
        {
            zLow = landZ;
            zCenter = landCenter;
            zTop = landTop;
            isSet = true;
        }

        foreach (var tile in _mapService.GetStatics(map, from.X, from.Y))
        {
            var item = _tileDataService.GetItem(tile.Id);
            var calcTop = tile.Z + item.StandHeight;
            var surface = (item.Flags & TileFlagType.Surface) != 0;
            var wet = (item.Flags & TileFlagType.Wet) != 0;

            if (isSet && calcTop < zCenter || from.Z < calcTop || !surface && !(canSwim && wet))
            {
                continue;
            }

            if (cantWalk && !wet)
            {
                continue;
            }

            zLow = tile.Z;
            zCenter = calcTop;

            var top = tile.Z + item.Height;

            if (!isSet || top > zTop)
            {
                zTop = top;
            }

            isSet = true;
        }

        if (!isSet)
        {
            zLow = zTop = from.Z;
        }
        else if (from.Z > zTop)
        {
            zTop = from.Z;
        }
    }

    // ModernUO MovementImpl.Check: the height the mover lands at in cell (x, y), preferring the one closest to fromZ.
    private bool Check(
        MapType map,
        int x,
        int y,
        int fromZ,
        int startZ,
        int startTop,
        MovementAbilityType ability,
        out int newZ
    )
    {
        newZ = 0;

        var canSwim = (ability & MovementAbilityType.Swim) != 0;
        var cantWalk = (ability & MovementAbilityType.Walk) == 0;
        var land = _mapService.GetLand(map, x, y);
        var landBlocks = LandBlocks(land, canSwim, cantWalk);
        var considerLand = !LandHeights.IsIgnored(land.Id);
        var statics = _mapService.GetStatics(map, x, y);

        LandHeights.Get(_mapService, map, x, y, out var landZ, out var landCenter, out _);

        var moveIsOk = false;
        var stepTop = startTop + StepHeight;
        var checkTop = startZ + PersonHeight;
        int testTop;

        foreach (var tile in statics)
        {
            var item = _tileDataService.GetItem(tile.Id);
            var notWater = (item.Flags & TileFlagType.Wet) == 0;
            var surface = (item.Flags & TileFlagType.Surface) != 0;
            var impassable = (item.Flags & TileFlagType.Impassable) != 0;

            // To move we must satisfy: a passable surface and the mover walks, or water and the mover swims.
            if ((!surface || impassable) && (!canSwim || notWater) || cantWalk && notWater)
            {
                continue;
            }

            int itemZ = tile.Z;
            var itemTop = itemZ;
            var ourZ = itemZ + item.StandHeight;
            testTop = checkTop;

            if (moveIsOk)
            {
                var cmp = Math.Abs(ourZ - fromZ) - Math.Abs(newZ - fromZ);

                if (cmp > 0 || cmp == 0 && ourZ > newZ)
                {
                    continue;
                }
            }

            if (ourZ + PersonHeight > testTop)
            {
                testTop = ourZ + PersonHeight;
            }

            if ((item.Flags & TileFlagType.Bridge) == 0)
            {
                itemTop += item.Height;
            }

            if (stepTop < itemTop)
            {
                continue;
            }

            var landCheck = itemZ + Math.Min((int)item.Height, StepHeight);

            if (considerLand && landCheck < landCenter && landCenter > ourZ && testTop > landZ)
            {
                continue;
            }

            if (IsOk(statics, ourZ, testTop))
            {
                newZ = ourZ;
                moveIsOk = true;
            }
        }

        if (!considerLand || landBlocks || stepTop < landZ)
        {
            return moveIsOk;
        }

        testTop = checkTop;

        if (landCenter + PersonHeight > testTop)
        {
            testTop = landCenter + PersonHeight;
        }

        var shouldCheck = true;

        if (moveIsOk)
        {
            var cmp = Math.Abs(landCenter - fromZ) - Math.Abs(newZ - fromZ);

            if (cmp > 0 || cmp == 0 && landCenter > newZ)
            {
                shouldCheck = false;
            }
        }

        if (shouldCheck && IsOk(statics, landCenter, testTop))
        {
            newZ = landCenter;
            moveIsOk = true;
        }

        return moveIsOk;
    }

    // ModernUO MovementImpl.IsOk: no impassable or surface static overlaps the space from ourZ to ourTop.
    private bool IsOk(IReadOnlyList<MapStaticTile> statics, int ourZ, int ourTop)
    {
        foreach (var tile in statics)
        {
            var item = _tileDataService.GetItem(tile.Id);

            if ((item.Flags & (TileFlagType.Impassable | TileFlagType.Surface)) == 0)
            {
                continue;
            }

            var checkZ = tile.Z;
            var checkTop = checkZ + item.StandHeight;

            if (checkTop > ourZ && ourTop > checkZ)
            {
                return false;
            }
        }

        return true;
    }

    // Impassable land blocks, except water for a swimmer; a mover that cannot walk is blocked by any other land.
    private bool LandBlocks(MapLandTile land, bool canSwim, bool cantWalk)
    {
        var flags = _tileDataService.GetLand(land.Id & 0x3FFF).Flags;
        var impassable = (flags & TileFlagType.Impassable) != 0;

        return (cantWalk || impassable) && !(impassable && canSwim && (flags & TileFlagType.Wet) != 0);
    }
}
