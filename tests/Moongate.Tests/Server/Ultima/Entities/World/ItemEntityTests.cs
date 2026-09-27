using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class ItemEntityTests
{
    private static readonly Serial Backpack = new(0x40000001);
    private static readonly Serial Mobile = new(0x00000010);

    [Fact]
    public void NewItem_IsNowhere()
    {
        Assert.Equal(ItemLocationType.None, new ItemEntity().Location);
    }

    [Fact]
    public void PlaceOnGround_SetsOnlyTheGroundGroup()
    {
        var item = InContainer();

        item.PlaceOnGround(MapType.Trammel, new Point3D(1602, 1591, 20));

        Assert.Equal(ItemLocationType.Ground, item.Location);
        Assert.Equal(MapType.Trammel, item.Map);
        Assert.Equal(new Point3D(1602, 1591, 20), item.GroundLocation);
        Assert.Null(item.ContainerId);
        Assert.Null(item.GridLocation);
        Assert.Null(item.MobileId);
        Assert.Null(item.Layer);
    }

    [Fact]
    public void PutInContainer_SetsOnlyTheContainerGroup()
    {
        var item = new ItemEntity { Id = new(0x40000002) };
        item.PlaceOnGround(MapType.Felucca, new Point3D(1, 2, 3));

        item.PutInContainer(Backpack, new Point2D(44, 65));

        Assert.Equal(ItemLocationType.Container, item.Location);
        Assert.Equal(Backpack, item.ContainerId);
        Assert.Equal(new Point2D(44, 65), item.GridLocation);
        Assert.Null(item.Map);
        Assert.Null(item.X);
        Assert.Null(item.MobileId);
    }

    [Fact]
    public void Equip_AfterBeingInAContainer_ClearsTheContainer()
    {
        var item = InContainer();

        item.Equip(Mobile, LayerType.OneHanded);

        Assert.Equal(ItemLocationType.Equipped, item.Location);
        Assert.Equal(Mobile, item.MobileId);
        Assert.Equal(LayerType.OneHanded, item.Layer);
        Assert.Null(item.ContainerId);
        Assert.Null(item.GridLocation);
        Assert.Null(item.Map);
    }

    [Fact]
    public void PutInContainer_ItsOwnId_Throws()
    {
        var item = new ItemEntity { Id = Backpack };

        Assert.Throws<ArgumentException>(() => item.PutInContainer(Backpack, new Point2D(0, 0)));
    }

    [Theory, InlineData(40000, 10), InlineData(10, -40000)]
    public void PutInContainer_AGridPointOutsideShort_Throws(int x, int y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemEntity { Id = new(0x40000003) }.PutInContainer(Backpack, new Point2D(x, y)));
    }

    [Fact]
    public void PutInContainer_AMobileSerial_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ItemEntity { Id = new(0x40000003) }.PutInContainer(Mobile, new Point2D(0, 0)));
    }

    [Theory, InlineData(0x40000001u), InlineData(0u)]
    public void Equip_NotAMobileSerial_Throws(uint serial)
    {
        Assert.Throws<ArgumentException>(() => new ItemEntity().Equip(new Serial(serial), LayerType.Helm));
    }

    [Fact]
    public void Equip_TheNoneLayer_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ItemEntity().Equip(Mobile, LayerType.None));
    }

    private static ItemEntity InContainer()
    {
        var item = new ItemEntity { Id = new(0x40000005) };
        item.PutInContainer(Backpack, new Point2D(10, 20));

        return item;
    }
}
