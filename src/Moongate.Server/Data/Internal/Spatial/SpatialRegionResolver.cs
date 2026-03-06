using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Data.Internal.Spatial;

/// <summary>
/// Resolves regions by map/location and provides region-derived values such as music.
/// </summary>
internal sealed class SpatialRegionResolver
{
    private readonly Lock _sync = new();
    private readonly List<JsonRegion> _regions = [];
    private readonly Dictionary<(int MapId, int SectorX, int SectorY), List<JsonRegion>> _regionsBySector = [];
    private readonly Dictionary<JsonRegion, int> _regionChildLevels = [];
    private bool _regionIndexDirty = true;

    public void AddRegion(JsonRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        lock (_sync)
        {
            _regions.Add(region);
            _regionIndexDirty = true;
        }
    }

    public int GetMusic(int mapId, Point3D location)
    {
        lock (_sync)
        {
            var regions = GetCandidateRegionsUnsafe(mapId, location);

            foreach (var region in regions)
            {
                if (region.MapId != mapId)
                {
                    continue;
                }

                if (!region.Area.Any(coordinate => coordinate.Contains(location.X, location.Y)))
                {
                    continue;
                }

                if (region.Music.HasValue)
                {
                    return (int)region.Music.Value;
                }
            }
        }

        return 0;
    }

    public JsonRegion? GetRegionById(int regionId)
    {
        lock (_sync)
        {
            return _regions.FirstOrDefault(region => region.Id == regionId);
        }
    }

    public JsonRegion? ResolveRegion(int mapId, Point3D location)
    {
        lock (_sync)
        {
            var regions = GetCandidateRegionsUnsafe(mapId, location);

            return regions.FirstOrDefault(
                region => region.MapId == mapId &&
                          region.Area.Any(coordinate => coordinate.Contains(location.X, location.Y))
            );
        }
    }

    private int CompareRegionOrderUnsafe(JsonRegion left, JsonRegion right)
    {
        var byPriority = right.Priority.CompareTo(left.Priority);

        if (byPriority != 0)
        {
            return byPriority;
        }

        var leftLevel = _regionChildLevels.TryGetValue(left, out var ll) ? ll : 0;
        var rightLevel = _regionChildLevels.TryGetValue(right, out var rl) ? rl : 0;

        return rightLevel.CompareTo(leftLevel);
    }

    private int ComputeChildLevelUnsafe(
        JsonRegion region,
        IReadOnlyDictionary<(int MapId, string Name), JsonRegion> byName,
        HashSet<JsonRegion> visiting
    )
    {
        if (_regionChildLevels.TryGetValue(region, out var cached))
        {
            return cached;
        }

        if (!visiting.Add(region))
        {
            return 0;
        }

        var level = 0;

        if (region is JsonTownRegion town &&
            town.Parent is not null &&
            byName.TryGetValue((town.Parent.MapId, town.Parent.Name), out var parent))
        {
            level = ComputeChildLevelUnsafe(parent, byName, visiting) + 1;
        }

        visiting.Remove(region);
        _regionChildLevels[region] = level;

        return level;
    }

    private void EnsureRegionIndexUnsafe()
    {
        if (!_regionIndexDirty)
        {
            return;
        }

        _regionsBySector.Clear();
        _regionChildLevels.Clear();

        var byName = _regions
                     .Where(static region => !string.IsNullOrWhiteSpace(region.Name))
                     .GroupBy(static region => (region.MapId, region.Name), static region => region)
                     .ToDictionary(static group => group.Key, static group => group.First());

        foreach (var region in _regions)
        {
            _ = ComputeChildLevelUnsafe(region, byName, []);
        }

        foreach (var region in _regions)
        {
            foreach (var coordinate in region.Area)
            {
                var minX = Math.Min(coordinate.X1, coordinate.X2) >> MapSectorConsts.SectorShift;
                var maxX = Math.Max(coordinate.X1, coordinate.X2) >> MapSectorConsts.SectorShift;
                var minY = Math.Min(coordinate.Y1, coordinate.Y2) >> MapSectorConsts.SectorShift;
                var maxY = Math.Max(coordinate.Y1, coordinate.Y2) >> MapSectorConsts.SectorShift;

                for (var sectorX = minX; sectorX <= maxX; sectorX++)
                {
                    for (var sectorY = minY; sectorY <= maxY; sectorY++)
                    {
                        var key = (region.MapId, sectorX, sectorY);

                        if (!_regionsBySector.TryGetValue(key, out var list))
                        {
                            list = [];
                            _regionsBySector[key] = list;
                        }

                        if (!list.Contains(region))
                        {
                            list.Add(region);
                        }
                    }
                }
            }
        }

        foreach (var list in _regionsBySector.Values)
        {
            list.Sort(CompareRegionOrderUnsafe);
        }

        _regionIndexDirty = false;
    }

    private List<JsonRegion> GetCandidateRegionsUnsafe(int mapId, Point3D location)
    {
        EnsureRegionIndexUnsafe();
        var (sectorX, sectorY) = GetSectorCoordinates(location);

        if (_regionsBySector.TryGetValue((mapId, sectorX, sectorY), out var bySector))
        {
            return bySector;
        }

        return _regions;
    }

    private static (int X, int Y) GetSectorCoordinates(Point3D location)
        => (location.X >> MapSectorConsts.SectorShift, location.Y >> MapSectorConsts.SectorShift);
}
