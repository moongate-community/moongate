using Moongate.UO.Data.Locations;

namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>In-memory registry of per-facet travel/location trees, queryable by facet name.</summary>
public interface ILocationService
{
    /// <summary>All registered facet roots, ordered by name.</summary>
    IReadOnlyList<LocationCategory> Facets { get; }

    /// <summary>Number of registered facets.</summary>
    int Count { get; }

    /// <summary>Returns the facet root with the given name (case-insensitive), or null.</summary>
    LocationCategory? GetFacet(string name);

    /// <summary>Adds or replaces a facet root, indexed by name.</summary>
    void Register(LocationCategory facet);
}
