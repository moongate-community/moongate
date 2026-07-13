using Moongate.Server.Interfaces.World;
using Moongate.UO.Data.Locations;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory registry of per-facet travel/location trees. Populated at startup by
/// <see cref="Moongate.Server.Loaders.LocationsLoader" />.
/// </summary>
public sealed class LocationService : ILocationService
{
    private readonly Dictionary<string, LocationCategory> _byFacet = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LocationCategory> Facets => [.. _byFacet.Values.OrderBy(facet => facet.Name)];

    public int Count => _byFacet.Count;

    public void Register(LocationCategory facet)
    {
        _byFacet[facet.Name] = facet;
    }

    public LocationCategory? GetFacet(string name)
    {
        return _byFacet.GetValueOrDefault(name);
    }
}
