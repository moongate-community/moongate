using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class RegionServiceTests
{
    [Fact]
    public void Find_TheHighestPriorityCoveringThePoint_Wins()
    {
        var service = Service(
            Region("Travel zone", 0, Area(0, 0, 4000, 4000)),
            Region("Britain", 50, Area(1400, 1500, 1700, 1800))
        );

        Assert.Equal("Britain", service.Find(MapType.Trammel, new Point3D(1500, 1600, 0))?.Name);
        Assert.Equal("Travel zone", service.Find(MapType.Trammel, new Point3D(100, 100, 0))?.Name);
    }

    [Fact]
    public void Find_OnATie_TheChildWinsOverItsParent_WhateverTheFileOrder()
    {
        var service = Service(
            Region("Britain Graveyard", 50, Area(1330, 1440, 1400, 1500), parent: "Britain"),
            Region("Britain", 50, Area(1200, 1400, 1700, 1800))
        );

        Assert.Equal("Britain Graveyard", service.Find(MapType.Trammel, new Point3D(1350, 1450, 0))?.Name);
        Assert.Equal("Britain", service.Find(MapType.Trammel, new Point3D(1600, 1600, 0))?.Name);
    }

    [Fact]
    public void Find_RespectsTheHeightOfAnArea()
    {
        var service = Service(
            Region("Outside", 50, Area(0, 0, 100, 100)),
            Region("Cellar", 60, new RegionAreaContent { X1 = 0, Y1 = 0, X2 = 100, Y2 = 100, Z1 = -20, Z2 = 0 })
        );

        Assert.Equal("Cellar", service.Find(MapType.Trammel, new Point3D(50, 50, -10))?.Name);
        Assert.Equal("Outside", service.Find(MapType.Trammel, new Point3D(50, 50, 0))?.Name);
    }

    [Fact]
    public void Find_TheSecondCornerIsExcluded_AndABigAreaIsFoundInEveryCell()
    {
        var service = Service(Region("Big", 50, Area(10, 10, 1000, 1000)));

        Assert.Equal("Big", service.Find(MapType.Trammel, new Point3D(999, 999, 0))?.Name);
        Assert.Null(service.Find(MapType.Trammel, new Point3D(1000, 999, 0)));
        Assert.Null(service.Find(MapType.Trammel, new Point3D(9, 500, 0)));
    }

    [Fact]
    public void Find_AnotherMapOrAPointOutsideTheGrid_FindsNothing()
    {
        var service = Service(Region("Britain", 50, Area(0, 0, 100, 100)));

        Assert.Null(service.Find(MapType.Felucca, new Point3D(50, 50, 0)));
        Assert.Null(service.Find(MapType.Trammel, new Point3D(-5, 50, 0)));
        Assert.Null(service.Find(MapType.Trammel, new Point3D(100000, 50, 0)));
    }

    private static RegionService Service(params RegionContent[] regions)
    {
        return new(new StubDataLoaderService().With(regions));
    }

    private static RegionContent Region(string name, int priority, RegionAreaContent area, string? parent = null)
    {
        return new() { Map = MapType.Trammel, Name = name, Priority = priority, Parent = parent, Areas = [area] };
    }

    private static RegionAreaContent Area(int x1, int y1, int x2, int y2)
    {
        return new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
    }
}
