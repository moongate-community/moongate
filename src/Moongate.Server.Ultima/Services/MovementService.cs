using Moongate.Core.Geometry;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Tiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Movement;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     A port of ModernUO's <c>MovementImpl.CheckMovement</c> and <c>Map.GetAverageZ</c> over terrain, statics and the
///     items lying on the ground: an impassable item in the mover's way blocks, a closed door among them, and a surface
///     item that cannot be picked up can be stood on. Mobiles, multis and ModernUO's special cases are not considered.
/// </summary>
public class MovementService : IMovementService
{
    private const int PersonHeight = 16;
    private const int StepHeight = 2;
    private const int LandIdMask = 0x3FFF;
    private const int CannotLiftWeight = 255;

    private readonly IMapService _mapService;
    private readonly ITileDataService _tileDataService;
    private readonly ISectorService? _sectors;
    private readonly IItemTemplateService? _templates;

    // What stands on the cell being checked, statics then ground items; reused, the game loop checks one cell at a time.
    private readonly List<CellTile> _cell = [];

    public MovementService(
        IMapService mapService,
        ITileDataService tileDataService,
        ISectorService? sectors = null,
        IItemTemplateService? templates = null
    )
    {
        _mapService = mapService;
        _tileDataService = tileDataService;
        _sectors = sectors;
        _templates = templates;
    }

    public int GetAverageZ(MapType map, int x, int y)
    {
        LandHeights.Get(_mapService, map, x, y, out _, out var average, out _);

        return average;
    }

    public bool TryGetDropZ(MapType map, int x, int y, int maxZ, out int z)
    {
        z = 0;

        if (!_mapService.Maps.Contains(map) || !_mapService.Contains(map, x, y))
        {
            return false;
        }

        // ModernUO Item.DropToWorld: the highest surface at or below the ceiling, from the land and the statics.
        var found = false;
        var land = _mapService.GetLand(map, x, y);

        if (!LandHeights.IsIgnored(land.Id) &&
            (_tileDataService.GetLand(land.Id & LandIdMask).Flags & TileFlagType.Impassable) == 0)
        {
            var average = GetAverageZ(map, x, y);

            if (average <= maxZ)
            {
                z = average;
                found = true;
            }
        }

        foreach (var tile in _mapService.GetStatics(map, x, y))
        {
            var item = _tileDataService.GetItem(tile.Id);

            if ((item.Flags & TileFlagType.Surface) == 0)
            {
                continue;
            }

            // StandHeight is already half the height for a bridge.
            var top = tile.Z + item.StandHeight;

            if (top <= maxZ && (!found || top > z))
            {
                z = top;
                found = true;
            }
        }

        return found;
    }

    public bool TryGetSpawnZ(MapType map, int x, int y, int maxZ, out int z)
    {
        z = 0;

        if (!_mapService.Maps.Contains(map) || !_mapService.Contains(map, x, y))
        {
            return false;
        }

        // UOX3 FindSpotForNPC: the highest surface under the ceiling a mobile can stand on, as ModernUO CanSpawnMobile.
        var found = false;
        var land = _mapService.GetLand(map, x, y);
        var statics = _mapService.GetStatics(map, x, y);

        if (!LandHeights.IsIgnored(land.Id) &&
            (_tileDataService.GetLand(land.Id & LandIdMask).Flags & (TileFlagType.Impassable | TileFlagType.Wet)) == 0)
        {
            var average = GetAverageZ(map, x, y);

            if (average <= maxZ && IsOk(statics, average, average + PersonHeight))
            {
                z = average;
                found = true;
            }
        }

        foreach (var tile in statics)
        {
            var item = _tileDataService.GetItem(tile.Id);

            if ((item.Flags & TileFlagType.Surface) == 0 ||
                (item.Flags & (TileFlagType.Impassable | TileFlagType.Wet)) != 0)
            {
                continue;
            }

            var top = tile.Z + item.StandHeight;

            if (top <= maxZ && (!found || top > z) && IsOk(statics, top, top + PersonHeight))
            {
                z = top;
                found = true;
            }
        }

        return found;
    }

    public bool TryGetSwimZ(MapType map, int x, int y, out int z)
    {
        z = 0;

        if (!_mapService.Maps.Contains(map) || !_mapService.Contains(map, x, y))
        {
            return false;
        }

        // UOX3's water spawn. Water is wet and impassable (so a walker cannot enter it) and flat; a wet static that is
        // passable or has a height, such as blood or a trough, is not water.
        var found = false;
        var land = _mapService.GetLand(map, x, y);
        var statics = _mapService.GetStatics(map, x, y);
        var considerLand = !LandHeights.IsIgnored(land.Id);
        var average = considerLand ? GetAverageZ(map, x, y) : int.MinValue;

        if (considerLand && IsWater(_tileDataService.GetLand(land.Id & LandIdMask).Flags))
        {
            if (IsOk(statics, average, average + PersonHeight))
            {
                z = average;
                found = true;
            }
        }

        foreach (var tile in statics)
        {
            var item = _tileDataService.GetItem(tile.Id);

            if (!IsWater(item.Flags) || item.Height != 0)
            {
                continue;
            }

            var top = tile.Z + item.StandHeight;

            // As MovementImpl's land check: water under the ground's centre cannot be moved on.
            if (top < average)
            {
                continue;
            }

            if ((!found || top > z) && IsOk(statics, top, top + PersonHeight))
            {
                z = top;
                found = true;
            }
        }

        return found;
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

        // As ModernUO, whatever is under the mover counts here, an item that can be picked up too.
        foreach (var tile in TilesAt(map, from.X, from.Y))
        {
            var item = tile.Tile;
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
        var ignoreDoors = (ability & MovementAbilityType.PassDoors) != 0;
        var openDoors = (ability & MovementAbilityType.OpenDoors) != 0;
        var tiles = TilesAt(map, x, y);

        LandHeights.Get(_mapService, map, x, y, out var landZ, out var landCenter, out _);

        var moveIsOk = false;
        var stepTop = startTop + StepHeight;
        var checkTop = startZ + PersonHeight;
        int testTop;

        // By index: IsOk reads the same list while this loop runs.
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            var item = tile.Tile;
            var notWater = (item.Flags & TileFlagType.Wet) == 0;
            var surface = (item.Flags & TileFlagType.Surface) != 0;
            var impassable = (item.Flags & TileFlagType.Impassable) != 0;

            // To move we must satisfy: a passable surface and the mover walks, or water and the mover swims; and an
            // item that can be picked up is no floor.
            if (!tile.Fixed || (!surface || impassable) && (!canSwim || notWater) || cantWalk && notWater)
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

            if (IsOk(tiles, ourZ, testTop, ignoreDoors, openDoors))
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

        if (shouldCheck && IsOk(tiles, landCenter, testTop, ignoreDoors, openDoors))
        {
            newZ = landCenter;
            moveIsOk = true;
        }

        return moveIsOk;
    }

    private static bool IsWater(TileFlagType flags)
    {
        return (flags & (TileFlagType.Wet | TileFlagType.Impassable)) == (TileFlagType.Wet | TileFlagType.Impassable);
    }

    // ModernUO MovementImpl.IsOk: no impassable or surface static or ground item overlaps the space from ourZ to
    // ourTop; a door does not count for a mover that passes doors, nor a closed and unlocked one for a mover that opens
    // them.
    private static bool IsOk(List<CellTile> tiles, int ourZ, int ourTop, bool ignoreDoors, bool openDoors)
    {
        foreach (var tile in tiles)
        {
            var item = tile.Tile;

            if ((item.Flags & (TileFlagType.Impassable | TileFlagType.Surface)) == 0)
            {
                continue;
            }

            if (tile.IsItem && (ignoreDoors || openDoors && tile.Openable) && Doors.IsDoor(item))
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

    // The same over the statics alone, for where something is placed rather than walked.
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

    // The statics of the cell, then the items lying on its ground.
    private List<CellTile> TilesAt(MapType map, int x, int y)
    {
        _cell.Clear();

        foreach (var tile in _mapService.GetStatics(map, x, y))
        {
            _cell.Add(new(_tileDataService.GetItem(tile.Id), tile.Z, false, true));
        }

        if (_sectors is null)
        {
            return _cell;
        }

        // By index: enumerating the list through its interface would allocate on every cell with an item.
        var items = _sectors.GetItemsAt(map, x, y);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            if (item.GroundLocation is { } spot && _tileDataService.TryGetItem(item.ItemId, out var data))
            {
                _cell.Add(new(data, spot.Z, true, IsFixed(item, data), Doors.IsDoor(data) && Doors.CanBeOpened(item, _templates)));
            }
        }

        return _cell;
    }

    // The item's own word, else its template's, else its graphic's: a tiledata weight of 255 cannot be lifted.
    private bool IsFixed(ItemEntity item, ItemTile data)
    {
        if (item.Movable is { } movable)
        {
            return !movable;
        }

        if (_templates is not null && _templates.TryGet(item.TemplateId, out var template))
        {
            return !template.EffectiveMovable(_tileDataService);
        }

        return data.Weight == CannotLiftWeight;
    }

    // Impassable land blocks, except water for a swimmer; a mover that cannot walk is blocked by any other land.
    private bool LandBlocks(MapLandTile land, bool canSwim, bool cantWalk)
    {
        var flags = _tileDataService.GetLand(land.Id & LandIdMask).Flags;
        var impassable = (flags & TileFlagType.Impassable) != 0;

        return (cantWalk || impassable) && !(impassable && canSwim && (flags & TileFlagType.Wet) != 0);
    }
}
