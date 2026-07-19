using Moongate.Core.Geometry;
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

    public RegionDefinition? At(MapType map, Point3D point)
    {
        RegionDefinition? best = null;

        foreach (var region in _regions)
        {
            if (region.Map != map)
            {
                continue;
            }

            var inArea = false;

            foreach (var rect in region.Area)
            {
                if (point.X >= rect.X1 && point.X <= rect.X2 && point.Y >= rect.Y1 && point.Y <= rect.Y2)
                {
                    inArea = true;

                    break;
                }
            }

            if (!inArea)
            {
                continue;
            }

            if (best is null || region.Priority > best.Priority)
            {
                best = region;
            }
        }

        return best;
    }

    public void Register(RegionDefinition region)
        => _regions.Add(region);
}
