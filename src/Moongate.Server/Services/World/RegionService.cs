using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

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

    public IReadOnlyList<RegionDefinition> ForMap(MapType map)
        => [.. _regions.Where(region => region.Map == map)];

    public void Register(RegionDefinition region)
        => _regions.Add(region);
}
