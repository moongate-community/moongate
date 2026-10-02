using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the public moongates of the maps the server loads, with their heights resolved, as ModernUO resolves the
///     height of Magincia from the map.
/// </summary>
public sealed class PublicMoongateService : IPublicMoongateService
{
    private readonly IDataLoaderService _data;
    private readonly ISectorService _sectors;
    private readonly IMovementService _movement;

    private IReadOnlyList<MoongateFacet>? _facets;

    public PublicMoongateService(IDataLoaderService data, ISectorService sectors, IMovementService movement)
    {
        _data = data;
        _sectors = sectors;
        _movement = movement;
    }

    public IReadOnlyList<MoongateFacet> GetFacets()
    {
        return _facets ??= Resolve();
    }

    private List<MoongateFacet> Resolve()
    {
        var facets = new List<MoongateFacet>();

        foreach (var facet in _data.GetEntities<MoongateFacet>())
        {
            // A spot outside the grid: the map is not loaded, or the data names a place it does not have.
            var destinations = facet.Destination
                                    .Where(destination => _sectors.IsInside(facet.Map, destination.Location.X, destination.Location.Y))
                                    .Select(destination => Resolve(facet, destination))
                                    .ToList();

            if (destinations.Count > 0)
            {
                facets.Add(
                    new()
                    {
                        Map = facet.Map,
                        Cliloc = facet.Cliloc,
                        SelectedCliloc = facet.SelectedCliloc,
                        Destination = destinations
                    }
                );
            }
        }

        return facets;
    }

    // A copy: the loaded data stays as the file wrote it.
    private MoongateDestination Resolve(MoongateFacet facet, MoongateDestination destination)
    {
        var location = destination.Location;

        return new()
        {
            Name = destination.Name,
            Cliloc = destination.Cliloc,
            Hue = destination.Hue,
            AverageZ = destination.AverageZ,
            Location = destination.AverageZ
                ? new Point3D(location.X, location.Y, _movement.GetAverageZ(facet.Map, location.X, location.Y))
                : location
        };
    }
}
