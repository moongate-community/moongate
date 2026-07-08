using Moongate.Server.Services;
using Moongate.UO.Data.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class RegionServiceTests
{
    private static RegionDefinition Region(MapType map, string name)
    {
        return new RegionDefinition { Type = "TownRegion", Map = map, Name = name };
    }

    [Fact]
    public void Register_CountsAll_AndFiltersByMap()
    {
        var service = new RegionService();
        service.Register(Region(MapType.Felucca, "Britain"));
        service.Register(Region(MapType.Trammel, "Britain"));

        Assert.Equal(2, service.Count);
        Assert.Single(service.ForMap(MapType.Felucca));
        Assert.Equal("Britain", service.ForMap(MapType.Felucca)[0].Name);
    }

    [Fact]
    public void ForMap_Unknown_IsEmpty()
    {
        var service = new RegionService();
        service.Register(Region(MapType.Felucca, "Britain"));

        Assert.Empty(service.ForMap(MapType.Malas));
    }
}
