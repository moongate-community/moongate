using Moongate.Core.Geometry;
using Moongate.UO.Data.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>In-memory registry of map regions.</summary>
public interface IRegionService
{
    /// <summary>All registered regions in load order.</summary>
    IReadOnlyList<RegionDefinition> All { get; }

    /// <summary>Number of registered regions.</summary>
    int Count { get; }

    /// <summary>Returns the regions on the given map.</summary>
    IReadOnlyList<RegionDefinition> ForMap(MapType map);

    /// <summary>
    /// Resolves the highest-priority region on <paramref name="map" /> whose area contains
    /// <paramref name="point" />, or null when none matches.
    /// </summary>
    RegionDefinition? At(MapType map, Point3D point);

    /// <summary>Adds a region to the registry.</summary>
    void Register(RegionDefinition region);
}
