using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Sectors;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Sectors;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps one <see cref="SectorGrid" /> per map, with its mobiles and ground items, built from <c>maps.toml</c> the
///     first time the map is used, as ModernUO keeps its sector array per map.
/// </summary>
public sealed class SectorService : ISectorService
{
    public const int SectorSize = 16;
    private const int SectorShift = 4;

    // As ModernUO's Map.SectorActiveRange: a player wakes the 5×5 sectors around its own.
    private const int ActiveRange = 2;

    private readonly ILogger _logger = Log.ForContext<SectorService>();
    private readonly IDataLoaderService _data;
    private readonly WorldConfig _world;
    private readonly INpcTickService _ticks;
    private readonly Dictionary<MapType, SectorGrid?> _grids = [];
    private readonly Dictionary<Serial, Sector> _sectorOf = [];
    private readonly Dictionary<Serial, Sector> _itemSectorOf = [];

    // The ground items by cell, and the cell each one is filed under: the movement asks for a cell on every step.
    private readonly Dictionary<(MapType Map, int X, int Y), List<ItemEntity>> _itemsAt = [];
    private readonly Dictionary<Serial, (MapType Map, int X, int Y)> _itemCellOf = [];

    public SectorService(IDataLoaderService data, WorldConfig world, INpcTickService ticks)
    {
        _data = data;
        _world = world;
        _ticks = ticks;
    }

    public void Add(MobileEntity mobile)
    {
        var sector = GetOrCreateSector(mobile.Map, mobile.Location, mobile);

        if (sector is null)
        {
            return;
        }

        Enter(mobile, sector);
    }

    public void Remove(MobileEntity mobile)
    {
        if (_sectorOf.Remove(mobile.Id, out var sector))
        {
            sector.Mobiles.RemoveAll(other => other.Id == mobile.Id);
            WakeAround(sector, mobile, -1);
        }

        _ticks.Sleep(mobile);
    }

    public bool IsActive(MapType map, Point3D point)
    {
        var grid = GetGrid(map);

        if (grid is null || (uint)point.X >= (uint)grid.Width || (uint)point.Y >= (uint)grid.Height)
        {
            return false;
        }

        return grid.Cells[(point.Y >> SectorShift) * grid.Columns + (point.X >> SectorShift)] is { NearbyPlayers: > 0 };
    }

    public bool IsInside(MapType map, int x, int y)
    {
        return GetGrid(map) is { } grid && (uint)x < (uint)grid.Width && (uint)y < (uint)grid.Height;
    }

    public void Move(MobileEntity mobile)
    {
        var sector = GetOrCreateSector(mobile.Map, mobile.Location, mobile);
        _sectorOf.TryGetValue(mobile.Id, out var current);

        if (ReferenceEquals(current, sector))
        {
            return;
        }

        if (sector is null)
        {
            Remove(mobile);

            return;
        }

        // The new area wakes before the old one sleeps, so the sectors both cover never drop to zero players and
        // their NPCs keep their timers.
        Enter(mobile, sector);

        if (current is not null)
        {
            current.Mobiles.RemoveAll(other => other.Id == mobile.Id);
            WakeAround(current, mobile, -1);
        }
    }

    public IReadOnlyList<MobileEntity> GetMobilesInRange(MapType map, Point3D center, int range)
    {
        var found = new List<MobileEntity>();

        foreach (var sector in SectorsAround(map, center, range))
        {
            foreach (var mobile in sector.Mobiles)
            {
                if (Math.Abs(mobile.Location.X - center.X) <= range && Math.Abs(mobile.Location.Y - center.Y) <= range)
                {
                    found.Add(mobile);
                }
            }
        }

        return found;
    }

    public SectorQueryResult Query(MapType map, Point3D center, int? range = null)
    {
        var reach = range ?? _world.ViewRange;
        var players = new List<MobileEntity>();
        var npcs = new List<MobileEntity>();
        var items = new List<ItemEntity>();

        foreach (var sector in SectorsAround(map, center, reach))
        {
            foreach (var mobile in sector.Mobiles)
            {
                if (IsInRange(mobile.Location, center, reach))
                {
                    (mobile.IsNpc ? npcs : players).Add(mobile);
                }
            }

            foreach (var item in sector.Items)
            {
                if (item.GroundLocation is { } spot && IsInRange(spot, center, reach))
                {
                    items.Add(item);
                }
            }
        }

        return new(players, npcs, items);
    }

    public void AddItem(ItemEntity item)
    {
        if (item.Map is not { } map || item.GroundLocation is not { } location)
        {
            return;
        }

        var sector = GetOrCreateSector(map, location, item);

        if (sector is null)
        {
            return;
        }

        RemoveItem(item);
        sector.Items.Add(item);
        _itemSectorOf[item.Id] = sector;

        var cell = (map, location.X, location.Y);

        if (!_itemsAt.TryGetValue(cell, out var items))
        {
            items = _itemsAt[cell] = [];
        }

        items.Add(item);
        _itemCellOf[item.Id] = cell;
    }

    public void RemoveItem(ItemEntity item)
    {
        if (_itemSectorOf.Remove(item.Id, out var sector))
        {
            sector.Items.RemoveAll(other => other.Id == item.Id);
        }

        if (_itemCellOf.Remove(item.Id, out var cell) && _itemsAt.TryGetValue(cell, out var items))
        {
            items.RemoveAll(other => other.Id == item.Id);

            if (items.Count == 0)
            {
                _itemsAt.Remove(cell);
            }
        }
    }

    public IReadOnlyList<ItemEntity> GetItemsAt(MapType map, int x, int y)
    {
        return _itemsAt.TryGetValue((map, x, y), out var items) ? items : [];
    }

    public bool ContainsItem(ItemEntity item)
    {
        return _itemSectorOf.ContainsKey(item.Id);
    }

    public IReadOnlyList<ItemEntity> GetItemsInRange(MapType map, Point3D center, int range)
    {
        var found = new List<ItemEntity>();

        foreach (var sector in SectorsAround(map, center, range))
        {
            foreach (var item in sector.Items)
            {
                if (item.GroundLocation is { } location &&
                    Math.Abs(location.X - center.X) <= range &&
                    Math.Abs(location.Y - center.Y) <= range)
                {
                    found.Add(item);
                }
            }
        }

        return found;
    }

    // The sectors that exist among those overlapping the square around the center.
    private IEnumerable<Sector> SectorsAround(MapType map, Point3D center, int range)
    {
        var grid = GetGrid(map);

        if (grid is null)
        {
            yield break;
        }

        var fromX = Math.Max(center.X - range, 0) >> SectorShift;
        var fromY = Math.Max(center.Y - range, 0) >> SectorShift;
        var toX = Math.Min((center.X + range) >> SectorShift, grid.Columns - 1);
        var toY = Math.Min((center.Y + range) >> SectorShift, grid.Rows - 1);

        for (var sy = fromY; sy <= toY; sy++)
        {
            for (var sx = fromX; sx <= toX; sx++)
            {
                if (grid.Cells[sy * grid.Columns + sx] is { } sector)
                {
                    yield return sector;
                }
            }
        }
    }

    private Sector? GetOrCreateSector(MapType map, Point3D location, object owner)
    {
        var grid = GetGrid(map);

        if (grid is null || (uint)location.X >= (uint)grid.Width || (uint)location.Y >= (uint)grid.Height)
        {
            _logger.Warning("{Owner} at {Location} on {Map} is outside the sector grid", owner, location, map);

            return null;
        }

        var index = (location.Y >> SectorShift) * grid.Columns + (location.X >> SectorShift);

        return grid.Cells[index] ??= new(map, location.X >> SectorShift, location.Y >> SectorShift);
    }

    private SectorGrid? GetGrid(MapType map)
    {
        if (_grids.TryGetValue(map, out var grid))
        {
            return grid;
        }

        var content = _data.GetEntities<MapContent>().FirstOrDefault(entry => entry.Map == map);

        if (content is null)
        {
            _logger.Warning("Map {Map} has no maps.toml content: its mobiles stay out of the sector grid", map);
        }
        else
        {
            var columns = (content.Size.X + SectorSize - 1) >> SectorShift;
            var rows = (content.Size.Y + SectorSize - 1) >> SectorShift;
            grid = new(content.Size.X, content.Size.Y, columns, rows);
        }

        _grids[map] = grid;

        return grid;
    }

    private static bool IsInRange(Point3D point, Point3D center, int range)
    {
        return Math.Abs(point.X - center.X) <= range && Math.Abs(point.Y - center.Y) <= range;
    }

    private void Enter(MobileEntity mobile, Sector sector)
    {
        sector.Mobiles.Add(mobile);
        _sectorOf[mobile.Id] = sector;
        WakeAround(sector, mobile, 1);

        if (!mobile.IsNpc)
        {
            return;
        }

        if (sector.NearbyPlayers > 0)
        {
            _ticks.Wake(mobile);
        }
        else
        {
            _ticks.Sleep(mobile);
        }
    }

    // Only players keep sectors awake: the NPCs of a sector wake when its first nearby player arrives and sleep when
    // the last one leaves, as ModernUO's Sector.Activate and Deactivate.
    private void WakeAround(Sector sector, MobileEntity mobile, int change)
    {
        if (mobile.IsNpc || GetGrid(sector.Map) is not { } grid)
        {
            return;
        }

        var lastRow = Math.Min(sector.Y + ActiveRange, grid.Rows - 1);
        var lastColumn = Math.Min(sector.X + ActiveRange, grid.Columns - 1);

        for (var sy = Math.Max(sector.Y - ActiveRange, 0); sy <= lastRow; sy++)
        {
            for (var sx = Math.Max(sector.X - ActiveRange, 0); sx <= lastColumn; sx++)
            {
                var around = grid.Cells[sy * grid.Columns + sx] ??= new(sector.Map, sx, sy);
                around.NearbyPlayers += change;

                if (change > 0 && around.NearbyPlayers == 1)
                {
                    SetAwake(around, true);
                }
                else if (change < 0 && around.NearbyPlayers == 0)
                {
                    SetAwake(around, false);
                }
            }
        }
    }

    private void SetAwake(Sector sector, bool awake)
    {
        foreach (var mobile in sector.Mobiles)
        {
            if (!mobile.IsNpc)
            {
                continue;
            }

            if (awake)
            {
                _ticks.Wake(mobile);
            }
            else
            {
                _ticks.Sleep(mobile);
            }
        }
    }
}
