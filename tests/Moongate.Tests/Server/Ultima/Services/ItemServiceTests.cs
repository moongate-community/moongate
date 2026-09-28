using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemServiceTests
{
    private static readonly Serial Aria = new(0x00000002);

    private readonly StubMovementService _dropMovement = new() { DropZ = 3 };
    private readonly StubLineOfSightService _sight = new();
    private readonly MobileEntity _aria = new()
    {
        Id = new(2), Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1496, 1628, 10)
    };
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

    [Fact]
    public void MoveToContainer_PutsTheLiveItemInTheContainerAtThePosition()
    {
        var items = Service();

        items.MoveToContainer(_dagger, _bag.Id, new Point2D(12, 34));

        Assert.Equal((_bag.Id, new Point2D(12, 34)), (_dagger.ContainerId!.Value, _dagger.GridLocation!.Value));
        Assert.Equal([_coin, _dagger], items.GetContents(_bag.Id));
    }

    [Fact]
    public void Split_LeavesTheRestAsANewLiveItemWhereTheStackWas()
    {
        var items = Service();
        _coin.Amount = 100;
        _coin.Hue = new(0x0481);
        _coin.SetProp("minted", 3);

        var rest = items.Split(_coin, 30, new Serial(0x40000100));

        Assert.Equal(30, _coin.Amount);
        Assert.Equal(
            (new Serial(0x40000100), 70, _coin.TemplateId, _coin.ItemId, (ushort)0x0481, _bag.Id, new Point2D(30, 30), 3),
            (rest.Id, rest.Amount, rest.TemplateId, rest.ItemId, rest.Hue.Value, rest.ContainerId!.Value,
                rest.GridLocation!.Value, rest.GetProp<int>("minted"))
        );
        Assert.True(items.TryGet(rest.Id, out var live));
        Assert.Same(rest, live);
    }

    [Fact]
    public void Absorb_ForgetsTheItemAndQueuesItsDeletionForItsOwner()
    {
        var items = Service();

        items.Absorb(_coin);

        Assert.False(items.TryGet(_coin.Id, out _));
        Assert.Equal([_coin.Id], items.TombstonesOf(Aria));
        Assert.Equal([_coin.Id], ((IPersistenceDeletionSource)items).Capture());
    }

    [Fact]
    public void Committed_ClearsOnlyTheTombstonesThatWereSaved()
    {
        var items = Service();
        items.Absorb(_coin);
        var captured = ((IPersistenceDeletionSource)items).Capture();
        items.Absorb(_dagger);

        ((IPersistenceDeletionSource)items).Committed(captured);

        Assert.Equal([_dagger.Id], ((IPersistenceDeletionSource)items).Capture());
        Assert.Equal([_dagger.Id], items.TombstonesOf(Aria));
    }

    [Fact]
    public void TakeTombstonesOf_HandsThemOverAndTheSaveNoLongerSeesThem()
    {
        var items = Service();
        items.Absorb(_coin);

        Assert.Equal([_coin.Id], items.TakeTombstonesOf(Aria));

        Assert.Empty(items.TombstonesOf(Aria));
        Assert.Empty(((IPersistenceDeletionSource)items).Capture());
    }

    [Fact]
    public void Add_AnItemThatWasTombstoned_ClearsItsTombstone()
    {
        var items = Service();
        items.Absorb(_coin);

        items.Add([_coin]);

        Assert.Empty(((IPersistenceDeletionSource)items).Capture());
    }

    private ItemService Service()
    {
        var items = TestItems.Create();
        items.Add([_backpack, _bag, _coin, _dagger, _shirt, _ground]);

        return items;
    }

    [Fact]
    public void PlaceOnGround_PutsTheItemInTheGrid()
    {
        var sectors = TestSectors.Create();
        var items = TestItems.Create(sectors);
        var gold = Item(0x40000050);
        items.Add([gold]);

        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 5));

        Assert.Equal(new Point3D(1496, 1628, 5), gold.GroundLocation);
        Assert.Equal([gold], sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 0));
    }

    [Fact]
    public void Add_AGroundItem_PutsItInTheGrid()
    {
        var sectors = TestSectors.Create();
        var gold = Item(0x40000050);
        gold.PlaceOnGround(MapType.Trammel, new Point3D(1496, 1628, 0));

        TestItems.Create(sectors).Add([gold]);

        Assert.Equal([gold], sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 0));
    }

    [Fact]
    public void MoveToContainer_TakesAGroundItemOutOfTheGrid()
    {
        var sectors = TestSectors.Create();
        var items = TestItems.Create(sectors);
        var gold = Item(0x40000050);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 0));

        items.MoveToContainer(gold, new Serial(0x40000001), new Point2D(44, 65));

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void RemoveAndAbsorb_TakeGroundItemsOutOfTheGrid()
    {
        var sectors = TestSectors.Create();
        var items = TestItems.Create(sectors);
        var gold = Item(0x40000050);
        var silver = Item(0x40000051);
        items.Add([gold, silver]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 0));
        items.PlaceOnGround(silver, MapType.Trammel, new Point3D(1497, 1628, 0));

        items.Remove([gold.Id]);
        items.Absorb(silver);

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void HideAndShow_TakeAndPutBackWithoutMovingIt()
    {
        var sectors = TestSectors.Create();
        var items = TestItems.Create(sectors);
        var gold = Item(0x40000050);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 0));

        items.Hide(gold);

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
        Assert.Equal(new Point3D(1496, 1628, 0), gold.GroundLocation);

        items.Show(gold);

        Assert.Equal([gold], sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void Split_AGroundStack_PutsTheRestInTheGrid()
    {
        var sectors = TestSectors.Create();
        var items = TestItems.Create(sectors);
        var gold = Item(0x40000050);
        gold.Amount = 100;
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 0));

        var rest = items.Split(gold, 40, new Serial(0x40000060));

        Assert.Contains(rest, sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 0));
    }

    [Fact]
    public void TryDropOnGround_TwoTilesAway_PlacesItAtTheSurface()
    {
        var items = TestItems.Create(movement: _dropMovement, sight: _sight);
        var gold = Item(0x40000050);
        items.Add([gold]);

        Assert.True(items.TryDropOnGround(_aria, gold, 1498, 1626));

        Assert.Equal((MapType.Trammel, new Point3D(1498, 1626, 3)), (gold.Map!.Value, gold.GroundLocation!.Value));
        Assert.Equal((new Point3D(1496, 1628, 24), new Point3D(1498, 1626, 4)), _sight.Checks.Single());
    }

    [Theory]
    [InlineData(1499, 1628)]
    [InlineData(1496, 1625)]
    public void TryDropOnGround_ThreeTilesAway_Fails(int x, int y)
    {
        var items = TestItems.Create(movement: _dropMovement, sight: _sight);
        var gold = Item(0x40000050);
        items.Add([gold]);

        Assert.False(items.TryDropOnGround(_aria, gold, x, y));
        Assert.Null(gold.GroundLocation);
    }

    [Fact]
    public void TryDropOnGround_WithoutASurfaceOrLineOfSight_Fails()
    {
        var items = TestItems.Create(movement: _dropMovement, sight: _sight);
        var gold = Item(0x40000050);
        items.Add([gold]);
        _dropMovement.DropZ = null;

        Assert.False(items.TryDropOnGround(_aria, gold, 1497, 1628));

        _dropMovement.DropZ = 3;
        _sight.Allow = false;

        Assert.False(items.TryDropOnGround(_aria, gold, 1497, 1628));
        Assert.Null(gold.GroundLocation);
    }

    [Fact]
    public void CanReach_AGroundItemNearbyInSight_IsTrue()
    {
        var items = TestItems.Create(sight: _sight);
        var gold = Item(0x40000050);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1498, 1628, 10));

        Assert.True(items.CanReach(_aria, gold));

        _sight.Allow = false;

        Assert.False(items.CanReach(_aria, gold));
    }

    [Fact]
    public void CanReach_AGroundItemSomeoneHolds_IsFalse()
    {
        var items = TestItems.Create(sight: _sight);
        var gold = Item(0x40000050);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1497, 1628, 10));

        items.Hide(gold);

        Assert.False(items.CanReach(_aria, gold));
    }

    [Fact]
    public void CanReach_FarOrOnAnotherMapOrNotOnTheGround_IsFalse()
    {
        var items = TestItems.Create(sight: _sight);
        var gold = Item(0x40000050);
        items.Add([gold]);

        Assert.False(items.CanReach(_aria, gold));

        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1499, 1628, 10));
        Assert.False(items.CanReach(_aria, gold));

        items.PlaceOnGround(gold, MapType.Felucca, new Point3D(1497, 1628, 10));
        Assert.False(items.CanReach(_aria, gold));
    }

    private static ItemEntity Item(uint serial)
    {
        return new() { Id = new(serial), TemplateId = "item", ItemId = 0x0E75, Amount = 1 };
    }
}
