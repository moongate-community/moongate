using Moongate.UO.Data.Regions;

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

    /// <summary>Returns the regions on the given map (case-insensitive).</summary>
    IReadOnlyList<RegionDefinition> ForMap(string map);
}
