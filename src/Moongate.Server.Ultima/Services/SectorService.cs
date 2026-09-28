using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Sectors;
using Moongate.Server.Ultima.Data.Maps;
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

    private readonly ILogger _logger = Log.ForContext<SectorService>();
    private readonly IDataLoaderService _data;
    private readonly Dictionary<MapType, SectorGrid?> _grids = [];
    private readonly Dictionary<Serial, Sector> _sectorOf = [];
    private readonly Dictionary<Serial, Sector> _itemSectorOf = [];

    public SectorService(IDataLoaderService data)
    {
        _data = data;
    }

    public void Add(MobileEntity mobile)
    {
        var sector = GetOrCreateSector(mobile.Map, mobile.Location, mobile);

        if (sector is null)
        {
            return;
        }

        sector.Mobiles.Add(mobile);
        _sectorOf[mobile.Id] = sector;
    }

    public void Remove(MobileEntity mobile)
    {
        if (_sectorOf.Remove(mobile.Id, out var sector))
        {
            sector.Mobiles.RemoveAll(other => other.Id == mobile.Id);
        }
    }

    public void Move(MobileEntity mobile)
    {
        var sector = GetOrCreateSector(mobile.Map, mobile.Location, mobile);

        if (_sectorOf.TryGetValue(mobile.Id, out var current) && ReferenceEquals(current, sector))
        {
            return;
        }

        Remove(mobile);

        if (sector is not null)
        {
            sector.Mobiles.Add(mobile);
            _sectorOf[mobile.Id] = sector;
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
    }

    public void RemoveItem(ItemEntity item)
    {
        if (_itemSectorOf.Remove(item.Id, out var sector))
        {
            sector.Items.RemoveAll(other => other.Id == item.Id);
        }
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

        return grid.Cells[index] ??= new();
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
}
