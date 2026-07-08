using Moongate.Server.Services;
using Moongate.UO.Data.Regions;

namespace Moongate.Tests.Server;

public class RegionServiceTests
{
    private static RegionDefinition Region(string map, string name)
    {
        return new RegionDefinition { Type = "TownRegion", Map = map, Name = name };
    }

    [Fact]
    public void Register_CountsAll_AndFiltersByMap()
    {
        var service = new RegionService();
        service.Register(Region("Felucca", "Britain"));
        service.Register(Region("Trammel", "Britain"));

        Assert.Equal(2, service.Count);
        Assert.Single(service.ForMap("felucca"));
        Assert.Equal("Britain", service.ForMap("Felucca")[0].Name);
    }

    [Fact]
    public void ForMap_Unknown_IsEmpty()
    {
        var service = new RegionService();
        service.Register(Region("Felucca", "Britain"));

        Assert.Empty(service.ForMap("Malas"));
    }
}
