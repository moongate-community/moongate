using Moongate.Core.Geometry;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class RegionServiceTests
{
    [Fact]
    public void ForMap_Unknown_IsEmpty()
    {
        var service = new RegionService();
        service.Register(Region(MapType.Felucca, "Britain"));

        Assert.Empty(service.ForMap(MapType.Malas));
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
    public void At_PointInsideRegion_ReturnsRegion()
    {
        var service = new RegionService();
        service.Register(RegionWithArea(MapType.Felucca, "Britain", 0, 0, 10, 10));

        var found = service.At(MapType.Felucca, new(5, 5, 0));

        Assert.NotNull(found);
        Assert.Equal("Britain", found!.Name);
    }

    [Fact]
    public void At_PointOutsideRegion_ReturnsNull()
    {
        var service = new RegionService();
        service.Register(RegionWithArea(MapType.Felucca, "Britain", 0, 0, 10, 10));

        Assert.Null(service.At(MapType.Felucca, new(999, 999, 0)));
    }

    [Fact]
    public void At_WrongMap_ReturnsNull()
    {
        var service = new RegionService();
        service.Register(RegionWithArea(MapType.Felucca, "Britain", 0, 0, 10, 10));

        Assert.Null(service.At(MapType.Trammel, new(5, 5, 0)));
    }

    [Fact]
    public void At_OverlappingRegions_ReturnsHighestPriority()
    {
        var service = new RegionService();
        service.Register(RegionWithArea(MapType.Felucca, "Outer", 0, 0, 100, 100, priority: 1));
        service.Register(RegionWithArea(MapType.Felucca, "Inner", 40, 40, 60, 60, priority: 10));

        var found = service.At(MapType.Felucca, new(50, 50, 0));

        Assert.Equal("Inner", found!.Name);
    }

    [Fact]
    public void At_RegionMarkedImpassable_CarriesTheFlag()
    {
        var service = new RegionService();
        var region = RegionWithArea(MapType.Felucca, "Blocked", 0, 0, 10, 10);
        region.IsImpassable = true;
        service.Register(region);

        Assert.True(service.At(MapType.Felucca, new(5, 5, 0))!.IsImpassable);
    }

    private static RegionDefinition Region(MapType map, string name)
        => new() { Type = "TownRegion", Map = map, Name = name };

    private static RegionDefinition RegionWithArea(MapType map, string name, int x1, int y1, int x2, int y2, int priority = 0)
        => new()
        {
            Type = "TownRegion",
            Map = map,
            Name = name,
            Priority = priority,
            Area = [new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 }]
        };
}
