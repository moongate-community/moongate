using System.Collections.Concurrent;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Indexes the loaded regions by cells of 16×16 tiles, built on first use: each cell lists the regions overlapping it
///     in the order they apply, so a lookup checks only a few areas.
/// </summary>
public sealed class RegionService : IRegionService
{
    private const int CellShift = 4;
    private const string NoRegion = "no region";

    private readonly IDataLoaderService _data;
    private readonly Lazy<Dictionary<(MapType Map, int X, int Y), RegionContent[]>> _cells;
    private readonly ConcurrentDictionary<Serial, RegionContent?> _players = new();
    // Resolved on the first change: the listeners depend on services that depend on this one.
    private readonly Lazy<IEnumerable<IRegionChangeListener>>? _listeners;
    private readonly ILogger _logger;

    public RegionService(
        IDataLoaderService data,
        Lazy<IEnumerable<IRegionChangeListener>>? listeners = null,
        ILogger? logger = null
    )
    {
        _data = data;
        _listeners = listeners;
        _cells = new(Build);
        _logger = logger ?? Log.ForContext<RegionService>();
    }

    public RegionContent? Find(MapType map, Point3D location)
    {
        if (location.X < 0 || location.Y < 0 ||
            !_cells.Value.TryGetValue((map, location.X >> CellShift, location.Y >> CellShift), out var regions))
        {
            return null;
        }

        foreach (var region in regions)
        {
            if (region.Contains(location.X, location.Y, location.Z))
            {
                return region;
            }
        }

        return null;
    }

    public RegionContent? Current(Serial mobile)
    {
        return _players.GetValueOrDefault(mobile);
    }

    public void Entered(MobileEntity mobile)
    {
        if (mobile.IsNpc)
        {
            return;
        }

        var region = Find(mobile.Map, mobile.Location);
        _players[mobile.Id] = region;
        _logger.Debug("{Name} is in {Region:l}", mobile.Name, NameOf(region));
        Notify(mobile, null, region);
    }

    public void Moved(MobileEntity mobile)
    {
        if (!_players.TryGetValue(mobile.Id, out var previous))
        {
            return;
        }

        var region = Find(mobile.Map, mobile.Location);

        if (ReferenceEquals(region, previous))
        {
            return;
        }

        _players[mobile.Id] = region;
        _logger.Debug("{Name} left {Previous:l} for {Region:l}", mobile.Name, NameOf(previous), NameOf(region));
        Notify(mobile, previous, region);
    }

    public void Left(Serial mobile)
    {
        if (!_players.TryRemove(mobile, out _))
        {
            return;
        }

        foreach (var listener in _listeners?.Value ?? [])
        {
            listener.Left(mobile);
        }
    }

    private void Notify(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        foreach (var listener in _listeners?.Value ?? [])
        {
            listener.RegionChanged(player, previous, current);
        }
    }

    private static string NameOf(RegionContent? region)
    {
        return region is null ? NoRegion : region.Name ?? $"an unnamed {region.Type} region";
    }

    private Dictionary<(MapType, int, int), RegionContent[]> Build()
    {
        var regions = _data.GetEntities<RegionContent>();
        var lists = new Dictionary<(MapType, int, int), List<RegionContent>>();

        // The order they apply in: priority, then the child before its parent, then the file order.
        var ordered = regions.Select((region, index) => (region, index, depth: Depth(region, regions)))
                             .OrderByDescending(entry => entry.region.Priority)
                             .ThenByDescending(entry => entry.depth)
                             .ThenBy(entry => entry.index)
                             .Select(entry => entry.region);

        foreach (var region in ordered)
        {
            var cells = new HashSet<(MapType, int, int)>();

            foreach (var area in region.Areas.Where(area => area.X2 > area.X1 && area.Y2 > area.Y1))
            {
                for (var x = Math.Max(0, area.X1) >> CellShift; x <= (area.X2 - 1) >> CellShift; x++)
                {
                    for (var y = Math.Max(0, area.Y1) >> CellShift; y <= (area.Y2 - 1) >> CellShift; y++)
                    {
                        cells.Add((region.Map, x, y));
                    }
                }
            }

            foreach (var cell in cells)
            {
                if (!lists.TryGetValue(cell, out var list))
                {
                    lists[cell] = list = [];
                }

                list.Add(region);
            }
        }

        return lists.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    // How many parents the region has; the loader already refused missing parents and loops.
    private static int Depth(RegionContent region, IReadOnlyCollection<RegionContent> regions)
    {
        var depth = 0;
        var parent = region.Parent;

        while (parent is not null &&
               regions.FirstOrDefault(other => other.Map == region.Map && other.Name == parent) is { } found &&
               depth < regions.Count)
        {
            depth++;
            parent = found.Parent;
        }

        return depth;
    }
}
