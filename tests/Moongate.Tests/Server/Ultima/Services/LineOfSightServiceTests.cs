using Moongate.Core.Geometry;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LineOfSightServiceTests
{
    private readonly FakeMapService _map = new(64, 64);
    private readonly FakeTileDataService _tiles = new();
    private readonly LineOfSightConfig _config = new();

    public LineOfSightServiceTests()
    {
        _tiles.Item(0x64, TileFlagType.Impassable | TileFlagType.NoShoot, 20)
              .Item(0x65, TileFlagType.Wall | TileFlagType.Impassable, 20)
              .Item(0x66, TileFlagType.Window, 20)
              .Item(0x67, TileFlagType.NoShoot, 5)
              .Item(0x68, TileFlagType.Surface | TileFlagType.NoShoot, 0);
    }

    [Fact]
    public void HasLineOfSight_OpenLandAtEyeHeight_IsVisible()
    {
        AssertBothWays(true, new(2, 5, 14), new(10, 5, 14));
        AssertBothWays(true, new(2, 5, 14), new(9, 11, 14));
    }

    [Theory, InlineData(0x64, false), InlineData(0x65, true), InlineData(0x66, false), InlineData(0x67, true)]
    public void HasLineOfSight_StaticBetween_BlocksOnlyWithWindowOrNoShootAtRayHeight(int id, bool visible)
    {
        _map.AddStatic(6, 5, (ushort)id, 0);

        AssertBothWays(visible, new(2, 5, 14), new(10, 5, 14));
    }

    [Fact]
    public void HasLineOfSight_HillBetween_IsBlocked()
    {
        _map.SetLandZ(6, 0, 6, 63, 30);

        AssertBothWays(false, new(2, 5, 14), new(10, 5, 14));
    }

    [Fact]
    public void HasLineOfSight_GroundRay_IsBlockedByTheLand()
    {
        AssertBothWays(false, new(2, 5, 0), new(10, 5, 0));
    }

    [Fact]
    public void HasLineOfSight_GroundRayOverIgnoredLand_IsVisible()
    {
        _map.SetLandId(0, 0, 63, 63, 2);

        AssertBothWays(true, new(2, 5, 0), new(10, 5, 0));
    }

    [Fact]
    public void HasLineOfSight_LandRisingAtTheTarget_IsExemptOnlyForTheTarget()
    {
        // Only cell (10, 5) has a raised corner (11, 6); the target stands inside that land range.
        _map.SetLand(11, 6, 3, 20);

        Assert.True(Service().HasLineOfSight(MapType.Felucca, new(2, 5, 14), new(10, 5, 10)));
        Assert.False(Service().HasLineOfSight(MapType.Felucca, new(10, 5, 10), new(2, 5, 14)));
    }

    [Fact]
    public void HasLineOfSight_NoShootStaticAtTheTarget_IsExemptOnlyForTheTarget()
    {
        _map.AddStatic(10, 5, 0x64, 0);

        Assert.True(Service().HasLineOfSight(MapType.Felucca, new(2, 5, 14), new(10, 5, 14)));
        Assert.False(Service().HasLineOfSight(MapType.Felucca, new(10, 5, 14), new(2, 5, 14)));
    }

    [Fact]
    public void HasLineOfSight_VerticalLineThroughNoShootFloor_IsBlocked()
    {
        AssertBothWays(true, new(5, 5, 14), new(5, 5, 40));

        _map.AddStatic(5, 5, 0x68, 20);

        AssertBothWays(false, new(5, 5, 14), new(5, 5, 40));
    }

    [Fact]
    public void HasLineOfSight_DefaultDistance_Allows25Not26()
    {
        AssertBothWays(true, new(2, 5, 14), new(27, 5, 14));
        AssertBothWays(false, new(2, 5, 14), new(28, 5, 14));
    }

    [Fact]
    public void HasLineOfSight_ConfiguredDistance_Allows10Not11()
    {
        _config.MaxDistance = 10;

        AssertBothWays(true, new(2, 5, 14), new(12, 12, 14));
        AssertBothWays(false, new(2, 5, 14), new(13, 5, 14));
    }

    [Fact]
    public void HasLineOfSight_SamePoint_IsVisible()
    {
        Assert.True(Service().HasLineOfSight(MapType.Felucca, new(5, 5, 0), new(5, 5, 0)));
    }

    [Fact]
    public void HasLineOfSight_PointOutsideTheMap_IsNotVisible()
    {
        AssertBothWays(false, new(60, 5, 14), new(64, 5, 14));
        AssertBothWays(false, new(2, 5, 14), new(-1, 5, 14));
    }

    [Fact]
    public void HasLineOfSight_MapNotLoaded_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => Service().HasLineOfSight(MapType.Trammel, new(2, 5, 14), new(10, 5, 14)));
    }

    private void AssertBothWays(bool visible, Point3D a, Point3D b)
    {
        Assert.Equal(visible, Service().HasLineOfSight(MapType.Felucca, a, b));
        Assert.Equal(visible, Service().HasLineOfSight(MapType.Felucca, b, a));
    }

    private LineOfSightService Service()
    {
        return new(_map, _tiles, _config);
    }
}
