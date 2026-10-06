using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PublicMoongateServiceTests
{
    private readonly StubMovementService _movement = new() { LandingZ = 31 };

    [Fact]
    public void GetFacets_GivesTheFacetsOfTheLoadedMapsOnly_InFileOrder()
    {
        // The test sectors hold Trammel, Felucca, Ilshenar and Malas: Tokuno is not loaded.
        var service = Service(
            Facet(MapType.Trammel, Britain()),
            Facet(MapType.Tokuno, Britain()),
            Facet(MapType.Felucca, Britain())
        );

        Assert.Equal([MapType.Trammel, MapType.Felucca], service.GetFacets().Select(facet => facet.Map));
    }

    [Fact]
    public void GetFacets_AMapWhoseFilesAreNotOpen_IsLeftOut()
    {
        // The fake map service opens Felucca only.
        var service = new PublicMoongateService(
            new StubDataLoaderService().With(Facet(MapType.Trammel, Britain()), Facet(MapType.Felucca, Britain())),
            TestSectors.Create(),
            _movement,
            new FakeMapService(16, 16)
        );

        Assert.Equal([MapType.Felucca], service.GetFacets().Select(facet => facet.Map));
    }

    [Fact]
    public void GetFacets_ADestinationOutsideItsMap_IsLeftOut()
    {
        var outside = new MoongateDestination { Name = "Nowhere", Cliloc = 1, Location = new Point3D(9000, 100, 0) };

        var facet = Assert.Single(Service(Facet(MapType.Trammel, Britain(), outside)).GetFacets());

        Assert.Equal(["Britain"], facet.Destination.Select(destination => destination.Name));
    }

    [Fact]
    public void GetFacets_AFacetWithNoDestinationLeft_IsLeftOut()
    {
        var outside = new MoongateDestination { Name = "Nowhere", Cliloc = 1, Location = new Point3D(9000, 100, 0) };

        Assert.Empty(Service(Facet(MapType.Trammel, outside)).GetFacets());
    }

    [Fact]
    public void GetFacets_AnAverageZDestination_TakesItsHeightFromTheMap_WithoutChangingTheLoadedData()
    {
        var magincia = new MoongateDestination
        {
            Name = "Magincia", Cliloc = 1012010, Location = new Point3D(3563, 2139, 0), AverageZ = true, Hue = 5
        };

        var resolved = Assert.Single(Assert.Single(Service(Facet(MapType.Trammel, magincia)).GetFacets()).Destination);

        Assert.Equal(
            (new Point3D(3563, 2139, 31), "Magincia", 1012010, 5),
            (resolved.Location, resolved.Name, resolved.Cliloc, resolved.Hue)
        );
        Assert.Equal(new Point3D(3563, 2139, 0), magincia.Location);
    }

    [Fact]
    public void GetFacets_IsWorkedOutOnce()
    {
        var service = Service(Facet(MapType.Trammel, Britain()));

        Assert.Same(service.GetFacets(), service.GetFacets());
    }

    private PublicMoongateService Service(params MoongateFacet[] facets)
    {
        return new(new StubDataLoaderService().With(facets), TestSectors.Create(), _movement);
    }

    private static MoongateFacet Facet(MapType map, params MoongateDestination[] destinations)
    {
        return new() { Map = map, Cliloc = 1012000, SelectedCliloc = 1012012, Destination = [.. destinations] };
    }

    private static MoongateDestination Britain()
    {
        return new() { Name = "Britain", Cliloc = 1012004, Location = new Point3D(1336, 1997, 5) };
    }
}
