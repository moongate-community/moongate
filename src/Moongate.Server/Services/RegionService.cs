using Moongate.Server.Interfaces;
using Moongate.UO.Data.Regions;

namespace Moongate.Server.Services;

/// <summary>
/// In-memory registry of map regions. Populated at startup by
/// <see cref="Moongate.Server.Loaders.RegionsLoader" />. Regions are not uniquely keyed (names repeat
/// across maps), so they are held as an ordered list and queried by map.
/// </summary>
public sealed class RegionService : IRegionService
{
    private readonly List<RegionDefinition> _regions = [];

    public IReadOnlyList<RegionDefinition> All => _regions;

    public int Count => _regions.Count;

    public void Register(RegionDefinition region)
    {
        _regions.Add(region);
    }

    public IReadOnlyList<RegionDefinition> ForMap(string map)
    {
        return [.. _regions.Where(region => string.Equals(region.Map, map, StringComparison.OrdinalIgnoreCase))];
    }
}
