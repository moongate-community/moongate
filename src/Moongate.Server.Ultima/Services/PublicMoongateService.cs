using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

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
    private readonly IMapService? _maps;
    private readonly ILogger _logger = Log.ForContext<PublicMoongateService>();

    private IReadOnlyList<MoongateFacet>? _facets;

    public PublicMoongateService(
        IDataLoaderService data,
        ISectorService sectors,
        IMovementService movement,
        IMapService? maps = null
    )
    {
        _maps = maps;
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
            // A map whose files are not open has no gates, and its heights cannot be read.
            if (_maps is not null && !_maps.Maps.Contains(facet.Map))
            {
                continue;
            }

            var destinations = new List<MoongateDestination>();

            foreach (var destination in facet.Destination)
            {
                if (_sectors.IsInside(facet.Map, destination.Location.X, destination.Location.Y))
                {
                    destinations.Add(Resolve(facet, destination));
                }
                else
                {
                    _logger.Warning(
                        "Moongate {Name} of {Map} at {Location} is outside the map and is left out",
                        destination.Name,
                        facet.Map,
                        destination.Location
                    );
                }
            }

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
