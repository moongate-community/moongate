using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemServiceTests
{
    private static readonly Serial Aria = new(0x00000002);

    private readonly ItemEntity _backpack = Item(0x40000001);
    private readonly ItemEntity _bag = Item(0x40000002);
    private readonly ItemEntity _coin = Item(0x40000003);
    private readonly ItemEntity _dagger = Item(0x40000004);
    private readonly ItemEntity _shirt = Item(0x40000005);
    private readonly ItemEntity _ground = Item(0x40000006);

    public ItemServiceTests()
    {
        _backpack.Equip(Aria, LayerType.Backpack);
        _shirt.Equip(Aria, LayerType.Shirt);
        _bag.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _dagger.PutInContainer(_backpack.Id, new Point2D(60, 80));
        _coin.PutInContainer(_bag.Id, new Point2D(30, 30));
        _ground.PlaceOnGround(MapType.Trammel, new Point3D(1496, 1628, 10));
    }

    [Fact]
    public void Add_TryGetAndRemove_KeepAndForgetTheLiveItems()
    {
        var items = Service();

        Assert.True(items.TryGet(_bag.Id, out var bag));
        Assert.Same(_bag, bag);
        Assert.Equal(6, items.Items.Count);

        items.Remove([_bag.Id, _coin.Id]);

        Assert.False(items.TryGet(_bag.Id, out _));
        Assert.Equal(4, items.Items.Count);
    }

    [Fact]
    public void GetContents_ReturnsOnlyTheDirectChildrenInSerialOrder()
    {
        Assert.Equal([_bag, _dagger], Service().GetContents(_backpack.Id));
    }

    [Fact]
    public void GetOwner_OfAWornBackpack_IsTheWearer()
    {
        Assert.Equal(Aria, Service().GetOwner(_backpack));
    }

    [Fact]
    public void GetOwner_OfAnItemInABagInTheBackpack_IsTheWearer()
    {
        Assert.Equal(Aria, Service().GetOwner(_coin));
    }

    [Fact]
    public void GetOwner_OfAGroundItem_IsNobody()
    {
        Assert.Null(Service().GetOwner(_ground));
    }

    [Fact]
    public void GetOwner_InAContainerThatIsNotLive_IsNobody()
    {
        var items = Service();
        items.Remove([_backpack.Id]);

        Assert.Null(items.GetOwner(_coin));
    }

    [Fact]
    public void GetOwnedBy_ReturnsTheWornItemsAndEverythingInsideThem()
    {
        Assert.Equal(
            [_backpack.Id, _bag.Id, _coin.Id, _dagger.Id, _shirt.Id],
            Service().GetOwnedBy(Aria).Select(item => item.Id).Order()
        );
    }

    private ItemService Service()
    {
        var items = new ItemService();
        items.Add([_backpack, _bag, _coin, _dagger, _shirt, _ground]);

        return items;
    }

    private static ItemEntity Item(uint serial)
    {
        return new() { Id = new(serial), TemplateId = "item", ItemId = 0x0E75, Amount = 1 };
    }
}
