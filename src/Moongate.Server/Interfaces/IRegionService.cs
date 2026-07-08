using Moongate.UO.Data.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces;

/// <summary>In-memory registry of map regions.</summary>
public interface IRegionService
{
    /// <summary>All registered regions in load order.</summary>
    IReadOnlyList<RegionDefinition> All { get; }

    /// <summary>Number of registered regions.</summary>
    int Count { get; }

    /// <summary>Adds a region to the registry.</summary>
    void Register(RegionDefinition region);

    /// <summary>Returns the regions on the given map.</summary>
    IReadOnlyList<RegionDefinition> ForMap(MapType map);
}
