using Moongate.Server.Services.World;
using Moongate.UO.Data.Locations;

namespace Moongate.Tests.Server;

public class LocationServiceTests
{
    [Fact]
    public void Facets_IsOrderedByName()
    {
        var service = new LocationService();
        service.Register(Facet("Trammel"));
        service.Register(Facet("Felucca"));

        Assert.Equal(new[] { "Felucca", "Trammel" }, service.Facets.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void GetFacet_Unknown_ReturnsNull()
    {
        var service = new LocationService();

        Assert.Null(service.GetFacet("Nowhere"));
    }

    [Fact]
    public void Register_ThenGetFacet_IsCaseInsensitive()
    {
        var service = new LocationService();
        service.Register(Facet("Felucca"));

        Assert.Equal(1, service.Count);
        Assert.Equal("Felucca", service.GetFacet("felucca")!.Name);
        Assert.Equal(1592, service.GetFacet("Felucca")!.Locations[0].X);
    }

    private static LocationCategory Facet(string name)
        => new()
        {
            Name = name,
            Locations = [new() { Name = "Britain", X = 1592, Y = 1680, Z = 10 }]
        };
}
