using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SectorServiceTests
{
    [Fact]
    public void GetMobilesInRange_FindsAnAddedMobile()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);

        sectors.Add(aria);

        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Theory]
    [InlineData(18, 0, true)]
    [InlineData(0, 18, true)]
    [InlineData(-18, -18, true)]
    [InlineData(19, 0, false)]
    [InlineData(0, -19, false)]
    public void GetMobilesInRange_UsesTheSquareRangeAcrossSectors(int dx, int dy, bool expected)
    {
        var sectors = TestSectors.Create();
        // 1600 is a sector edge (100 * 16): the other mobile sits in another sector for every case.
        var other = Mobile(3, 1600 + dx, 1600 + dy);
        sectors.Add(other);

        var found = sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1600, 1600, 0), 18);

        Assert.Equal(expected, found.Contains(other));
    }

    [Fact]
    public void Move_AcrossASectorEdge_KeepsTheMobileFindableAndOnlyOnce()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1599, 1600);
        sectors.Add(aria);

        aria.Location = new Point3D(1600, 1600, 0);
        sectors.Move(aria);

        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1600, 1600, 0), 18));
        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1580, 1600, 0), 20));
    }

    [Fact]
    public void Move_FarAway_LeavesTheOldArea()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);
        sectors.Add(aria);

        aria.Location = new Point3D(3000, 3000, 0);
        sectors.Move(aria);

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(3000, 3000, 0), 18));
    }

    [Fact]
    public void Remove_ForgetsTheMobile()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);
        sectors.Add(aria);

        sectors.Remove(aria);

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void GetMobilesInRange_DoesNotMixMaps()
    {
        var sectors = TestSectors.Create();
        sectors.Add(Mobile(2, 1496, 1628, MapType.Felucca));

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void Add_OutsideTheMap_IsIgnored()
    {
        var sectors = TestSectors.Create();
        var lost = Mobile(2, 9000, 100);

        sectors.Add(lost);
        sectors.Move(lost);
        sectors.Remove(lost);

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(7160, 100, 0), 18));
    }

    [Fact]
    public void Add_OnAMapWithoutContent_IsIgnored()
    {
        var sectors = new SectorService(new StubDataLoaderService());

        sectors.Add(Mobile(2, 1496, 1628));

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void GetMobilesInRange_NearTheMapCorner_DoesNotThrow()
    {
        var sectors = TestSectors.Create();
        var corner = Mobile(2, 0, 0);
        sectors.Add(corner);

        Assert.Equal([corner], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(0, 0, 0), 18));
        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(7167, 4095, 0), 18));
    }

    [Fact]
    public void GetItemsInRange_FindsAGroundItem()
    {
        var sectors = TestSectors.Create();
        var gold = GroundItem(0x40000001, 1496, 1628);

        sectors.AddItem(gold);

        Assert.Equal([gold], sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496 + 19, 1628, 0), 18));
        Assert.Empty(sectors.GetItemsInRange(MapType.Felucca, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void RemoveItem_ForgetsIt()
    {
        var sectors = TestSectors.Create();
        var gold = GroundItem(0x40000001, 1496, 1628);
        sectors.AddItem(gold);

        sectors.RemoveItem(gold);
        sectors.RemoveItem(gold);

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void AddItem_NotOnTheGround_IsIgnored()
    {
        var sectors = TestSectors.Create();
        var coins = new ItemEntity { Id = new(0x40000002), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        coins.PutInContainer(new Serial(0x40000009), new Point2D(1, 1));

        sectors.AddItem(coins);

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(0, 0, 0), 7000));
    }

    [Fact]
    public void GetMobilesInRange_DoesNotReturnItems()
    {
        var sectors = TestSectors.Create();
        sectors.AddItem(GroundItem(0x40000001, 1496, 1628));

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    private static ItemEntity GroundItem(uint serial, int x, int y)
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        item.PlaceOnGround(MapType.Trammel, new Point3D(x, y, 0));

        return item;
    }

    private static MobileEntity Mobile(uint serial, int x, int y, MapType map = MapType.Trammel)
    {
        return new() { Id = new Serial(serial), Name = $"M{serial}", Map = map, Location = new Point3D(x, y, 0) };
    }
}
