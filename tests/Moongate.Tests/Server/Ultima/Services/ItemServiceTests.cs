using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Timing;
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

    // A character's rows are as its last save left them: an item another player took since, or one merged into a
    // stack since, must not come back with the character.
    [Fact]
    public void AddLoaded_LeavesOutWhatIsAlreadyLive_AndWhatIsQueuedForDeletion()
    {
        var items = Service();
        items.Absorb(_coin);
        var staleDagger = Item(_dagger.Id.Value);
        staleDagger.PutInContainer(new Serial(0x40000900), new Point2D(1, 1));
        var staleCoin = Item(_coin.Id.Value);
        staleCoin.PutInContainer(new Serial(0x40000900), new Point2D(2, 2));
        var fresh = Item(0x40000901);
        fresh.PutInContainer(new Serial(0x40000900), new Point2D(3, 3));

        var added = items.AddLoaded([staleDagger, staleCoin, fresh]);

        Assert.Equal([fresh], added);
        Assert.True(items.TryGet(_dagger.Id, out var live));
        Assert.Same(_dagger, live);
        Assert.Equal([_bag, _dagger], items.GetContents(_backpack.Id));
        Assert.False(items.TryGet(_coin.Id, out _));
        Assert.Equal([_coin.Id], items.TombstonesOf(Aria));
        Assert.Equal([fresh], items.GetContents(new Serial(0x40000900)));
    }

    [Fact]
    public void AddLoaded_WithNothingStale_AddsEverything()
    {
        var items = TestItems.Create();

        Assert.Equal(6, items.AddLoaded([_backpack, _bag, _coin, _dagger, _shirt, _ground]).Count);
        Assert.Equal(6, items.Items.Count);
    }

    [Fact]
    public void GetContents_OfAnUnknownContainerOrAnEmptyOne_IsEmpty()
    {
        var items = Service();

        Assert.Empty(items.GetContents(new Serial(0x40000999)));
        Assert.Empty(items.GetContents(_dagger.Id));
        Assert.Empty(items.GetContents(_ground.Id));
    }

    [Fact]
    public void GetContents_FollowsAnItemMovedToAnotherContainer()
    {
        var items = Service();

        items.MoveToContainer(_dagger, _bag.Id, new Point2D(1, 1));

        Assert.Equal([_bag], items.GetContents(_backpack.Id));
        Assert.Equal([_coin, _dagger], items.GetContents(_bag.Id));

        items.MoveToContainer(_dagger, _backpack.Id, new Point2D(1, 1));

        Assert.Equal([_bag, _dagger], items.GetContents(_backpack.Id));
        Assert.Equal([_coin], items.GetContents(_bag.Id));
    }

    [Fact]
    public void GetContents_FollowsAnItemMovedInsideItsOwnContainer()
    {
        var items = Service();

        items.MoveToContainer(_dagger, _backpack.Id, new Point2D(5, 5));

        Assert.Equal([_bag, _dagger], items.GetContents(_backpack.Id));
    }

    [Fact]
    public void GetContents_LosesAnItemPutOnTheGroundOrWorn_AndGainsOnePutIn()
    {
        var items = Service();

        items.PlaceOnGround(_dagger, MapType.Trammel, new Point3D(1497, 1628, 10));
        items.Equip(_bag, Aria, LayerType.Waist);

        Assert.Empty(items.GetContents(_backpack.Id));
        // What is inside a moved container stays inside it.
        Assert.Equal([_coin], items.GetContents(_bag.Id));

        items.MoveToContainer(_ground, _backpack.Id, new Point2D(1, 1));
        items.MoveToContainer(_shirt, _backpack.Id, new Point2D(2, 2));

        Assert.Equal([_shirt, _ground], items.GetContents(_backpack.Id));
    }

    [Fact]
    public void GetContents_LosesAnItemRemovedOrAbsorbed()
    {
        var items = Service();

        items.Remove([_dagger.Id]);
        items.Absorb(_bag);

        Assert.Empty(items.GetContents(_backpack.Id));
    }

    [Fact]
    public void GetContents_HasTheRestOfAStackSplitInAContainer()
    {
        var items = Service();
        _coin.Amount = 10;

        var rest = items.Split(_coin, 4, new Serial(0x40000050));

        Assert.Equal([_coin, rest], items.GetContents(_bag.Id));
    }

    [Fact]
    public void GetContents_AnItemAddedAgainAsANewInstance_IsListedOnce_WhereTheNewOneLies()
    {
        var items = Service();
        var reloaded = Item(_dagger.Id.Value);
        reloaded.PutInContainer(_bag.Id, new Point2D(1, 1));

        items.Add([reloaded]);

        Assert.Equal([_bag], items.GetContents(_backpack.Id));
        Assert.Equal([_coin, reloaded], items.GetContents(_bag.Id));
    }

    // An entity changed outside the service is wrong use, but must not leave a ghost in the old container.
    [Fact]
    public void GetContents_AnItemChangedOutsideTheServiceAndAddedAgain_IsListedOnce_WhereItLies()
    {
        var items = Service();

        _dagger.PutInContainer(_bag.Id, new Point2D(1, 1));
        Assert.Equal([_bag], items.GetContents(_backpack.Id));

        items.Add([_dagger]);

        Assert.Equal([_bag], items.GetContents(_backpack.Id));
        Assert.Equal([_coin, _dagger], items.GetContents(_bag.Id));
    }

    [Fact]
    public void GetContents_DoesNotLookAtTheOtherItemsOfTheWorld()
    {
        var items = Service();
        var others = Enumerable.Range(0, 100_000)
                               .Select(
                                   index =>
                                   {
                                       var item = Item(0x41000000u + (uint)index);
                                       item.PlaceOnGround(MapType.Trammel, new Point3D(100 + index % 1000, 100, 0));

                                       return item;
                                   }
                               )
                               .ToList();
        items.Add(others);
        var watch = System.Diagnostics.Stopwatch.StartNew();

        for (var index = 0; index < 5_000; index++)
        {
            items.GetContents(_backpack.Id);
        }

        // A scan of the 100,000 items at each call takes many seconds; the index, a few milliseconds.
        Assert.InRange(watch.ElapsedMilliseconds, 0, 1000);
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
    public void GetWornRoot_IsTheWornItemAtTheTop_AndNullOnTheGround()
    {
        var service = Service();

        Assert.Equal((_backpack, _backpack, null), (service.GetWornRoot(_coin), service.GetWornRoot(_backpack), service.GetWornRoot(_ground)));
    }

    [Fact]
    public void GetGroundRoot_IsTheGroundItemAtTheTop_AndNullWhenCarried()
    {
        var service = Service();
        var gem = Item(0x40000060);
        var box = Item(0x40000061);
        box.PutInContainer(_ground.Id, new Point2D(1, 1));
        gem.PutInContainer(box.Id, new Point2D(1, 1));
        service.Add([box, gem]);

        Assert.Equal((_ground, _ground, _ground), (service.GetGroundRoot(gem), service.GetGroundRoot(box), service.GetGroundRoot(_ground)));
        Assert.Equal((null, null), (service.GetGroundRoot(_coin), service.GetGroundRoot(_backpack)));
    }

    [Fact]
    public void GetGroundRoot_InAContainerThatIsNotLive_IsNull()
    {
        var service = Service();
        var gem = Item(0x40000060);
        gem.PutInContainer(new Serial(0x40000099), new Point2D(1, 1));
        service.Add([gem]);

        Assert.Null(service.GetGroundRoot(gem));
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
    public void AContainerThatChangesPlace_AsksTheSaveToWriteItsContentsAgain_UntilTheyAreCommitted()
    {
        var items = Service();
        var chest = Item(0x40000010);
        chest.PlaceOnGround(MapType.Trammel, new Point3D(1500, 1628, 10));
        items.Add([chest]);
        Assert.Empty(items.CaptureRewrites());

        // The bag holds the coin; the dagger holds nothing.
        items.MoveToContainer(_bag, chest.Id, new Point2D(10, 10));
        items.MoveToContainer(_dagger, chest.Id, new Point2D(20, 20));

        Assert.Equal([_coin.Id], items.CaptureRewrites());

        items.RewritesCommitted([_coin.Id]);
        Assert.Empty(items.CaptureRewrites());

        items.PlaceOnGround(_bag, MapType.Trammel, new Point3D(1501, 1628, 10));
        Assert.Equal([_coin.Id], items.CaptureRewrites());
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
    public void MoveToContainer_GivesTheItemAFreeSlot()
    {
        var items = Service();

        items.MoveToContainer(_dagger, _bag.Id, new Point2D(12, 34));

        Assert.NotEqual(_coin.GridIndex, _dagger.GridIndex);
    }

    [Fact]
    public void MoveToContainer_ToTheSlotTheClientAsksFor_KeepsItWhenFreeAndMovesOnWhenTaken()
    {
        var items = Service();
        var taken = _coin.GridIndex!.Value;

        items.MoveToContainer(_dagger, _bag.Id, new Point2D(12, 34), 9);
        Assert.Equal((short)9, _dagger.GridIndex);

        items.MoveToContainer(_dagger, _bag.Id, new Point2D(12, 34), taken);
        Assert.Equal((short)(taken + 1), _dagger.GridIndex);
    }

    [Fact]
    public void MoveToContainer_WithinTheSameContainer_MayKeepItsOwnSlot()
    {
        var items = Service();
        var own = _coin.GridIndex!.Value;

        items.MoveToContainer(_coin, _bag.Id, new Point2D(50, 50), own);

        Assert.Equal(own, _coin.GridIndex);
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
    public void Split_InAContainer_KeepsTheRestInTheSlotAndGivesTheHeldPartAnother()
    {
        // The held part bounces back into the same container when the drop fails: two stacks, two slots.
        var items = Service();
        _coin.Amount = 100;
        var slot = _coin.GridIndex;

        var rest = items.Split(_coin, 30, new Serial(0x40000100));

        Assert.Equal(slot, rest.GridIndex);
        Assert.NotEqual(slot, _coin.GridIndex);
        Assert.NotNull(_coin.GridIndex);
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
    public void Equip_PutsTheItemOnTheWearerAndInTheWornIndex()
    {
        var sectors = TestSectors.Create();
        var items = TestItems.Create(sectors);
        var sword = Item(0x40000050);
        items.Add([sword]);
        items.PlaceOnGround(sword, MapType.Trammel, new Point3D(1496, 1628, 0));

        items.Equip(sword, new Serial(0x00000002), LayerType.OneHanded);

        Assert.Equal((new Serial(0x00000002), LayerType.OneHanded), (sword.MobileId!.Value, sword.Layer!.Value));
        Assert.Null(sword.GroundLocation);
        Assert.Equal([sword], items.GetWorn(new Serial(0x00000002)));
        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void MoveToContainer_TakesAWornItemOutOfTheWornIndex()
    {
        var items = TestItems.Create();
        var sword = Item(0x40000050);
        sword.Equip(new Serial(0x00000002), LayerType.OneHanded);
        items.Add([sword]);

        items.MoveToContainer(sword, new Serial(0x40000001), new Point2D(44, 65));

        Assert.Empty(items.GetWorn(new Serial(0x00000002)));
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

    [Fact]
    public void Release_ThenTakeReleasedOf_GivesTheLiveItemsOnce()
    {
        var items = TestItems.Create();
        var gold = Item(0x40000050);
        var gone = Item(0x40000051);
        items.Add([gold, gone]);
        items.Release(gold, Aria);
        items.Release(gone, Aria);
        items.Remove([gone.Id]);

        Assert.Equal([gold], items.TakeReleasedOf(Aria));
        Assert.Empty(items.TakeReleasedOf(Aria));
    }

    [Fact]
    public void Absorb_WithAnOwner_QueuesTheDeletionForThatOwner()
    {
        var items = TestItems.Create();
        var gold = Item(0x40000050);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 0));

        items.Absorb(gold, Aria);

        Assert.Equal([gold.Id], items.TombstonesOf(Aria));
    }

    [Fact]
    public void GetWorn_GivesOnlyWhatTheMobileWears()
    {
        var items = TestItems.Create();
        var shirt = Item(0x40000050);
        var coins = Item(0x40000051);
        shirt.Equip(Aria, LayerType.Shirt);
        coins.PutInContainer(new Serial(0x40000052), new Point2D(1, 1));
        items.Add([shirt, coins]);

        Assert.Equal([shirt], items.GetWorn(Aria));

        items.Remove([shirt.Id]);

        Assert.Empty(items.GetWorn(Aria));
    }

    [Fact]
    public void IsLyingOnGround_IsFalseWhileHeld()
    {
        var items = TestItems.Create();
        var gold = Item(0x40000050);
        items.Add([gold]);
        items.PlaceOnGround(gold, MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.True(items.IsLyingOnGround(gold));

        items.Hide(gold);

        Assert.False(items.IsLyingOnGround(gold));
    }

    [Fact]
    public void Equip_AnItemFromABag_QueuesOnEquipOfItsScript()
    {
        var (items, scripts) = Scripted();

        items.Equip(_dagger, Aria, LayerType.OneHanded);

        Assert.Equal(["0x40000004 on_equip 2"], scripts.Queued);
    }

    [Fact]
    public void Equip_AWornItemAgain_RaisesNothing()
    {
        // A worn item lifted and bounced back never left its layer.
        var (items, scripts) = Scripted();

        items.Equip(_shirt, Aria, LayerType.Shirt);

        Assert.Empty(scripts.Queued);
    }

    [Fact]
    public void Equip_AnItemAnotherMobileWore_QueuesOnUnequipThenOnEquip()
    {
        var (items, scripts) = Scripted();

        items.Equip(_shirt, new Serial(3), LayerType.Shirt);

        Assert.Equal(["0x40000005 on_unequip 2", "0x40000005 on_equip 3"], scripts.Queued);
    }

    [Fact]
    public void MoveToContainerPlaceOnGroundAndAbsorb_OfAWornItem_QueueOnUnequip()
    {
        var (items, scripts) = Scripted();

        items.MoveToContainer(_shirt, _backpack.Id, new Point2D(10, 10));
        items.Equip(_dagger, Aria, LayerType.OneHanded);
        items.PlaceOnGround(_dagger, MapType.Trammel, new Point3D(1496, 1629, 10));
        items.Equip(_coin, Aria, LayerType.Ring);
        items.Absorb(_coin, Aria);

        Assert.Equal(
            [
                "0x40000005 on_unequip 2", "0x40000004 on_equip 2", "0x40000004 on_unequip 2", "0x40000003 on_equip 2",
                "0x40000003 on_unequip 2"
            ],
            scripts.Queued
        );
    }

    [Fact]
    public void MovingAnUnwornItem_RaisesNothing()
    {
        var (items, scripts) = Scripted();

        items.MoveToContainer(_dagger, _bag.Id, new Point2D(10, 10));
        items.PlaceOnGround(_coin, MapType.Trammel, new Point3D(1496, 1629, 10));

        Assert.Empty(scripts.Queued);
    }

    [Fact]
    public void Decay_AnItemPutOnTheGroundStartsDecaying_AndStopsWhenItLeavesIt()
    {
        var (items, clock) = Decaying();
        var now = clock.Now.UtcDateTime;

        items.PlaceOnGround(_dagger, MapType.Trammel, new Point3D(1496, 1629, 10));
        Assert.Equal(now.AddHours(1), _dagger.DecayAt);

        items.Hide(_dagger);
        Assert.Null(_dagger.DecayAt);

        clock.Advance(TimeSpan.FromMinutes(10));
        items.Show(_dagger);
        Assert.Equal(now.AddMinutes(70), _dagger.DecayAt);

        items.MoveToContainer(_dagger, _backpack.Id, new Point2D(10, 10));
        Assert.Null(_dagger.DecayAt);

        items.PlaceOnGround(_dagger, MapType.Trammel, new Point3D(1496, 1629, 10));
        items.Equip(_dagger, Aria, LayerType.OneHanded);
        Assert.Null(_dagger.DecayAt);
    }

    [Fact]
    public void Decay_AGroundItemAddedWithASavedTimeKeepsIt_OneWithoutGetsAFreshOne()
    {
        var (items, clock) = Decaying();
        var saved = Item(0x40000010);
        saved.PlaceOnGround(MapType.Trammel, new Point3D(1496, 1630, 10));
        saved.DecayAt = clock.Now.UtcDateTime.AddMinutes(-5);

        items.Add([saved]);

        Assert.Equal(clock.Now.UtcDateTime.AddMinutes(-5), saved.DecayAt);
        Assert.Equal(clock.Now.UtcDateTime.AddHours(1), _ground.DecayAt);
        Assert.Null(_dagger.DecayAt);
    }

    [Fact]
    public void Decay_TheRestOfASplitGroundStackKeepsItsTime()
    {
        var (items, clock) = Decaying();
        var stackTime = _ground.DecayAt;
        clock.Advance(TimeSpan.FromMinutes(10));

        var rest = items.Split(_ground, 1, new Serial(0x40000020));

        Assert.Equal(stackTime, rest.DecayAt);
    }

    [Fact]
    public void Absorb_AGroundItemAPlayerDropped_IsDeletedByThatPlayersSave()
    {
        // Its row still says the dropper carries it: without the dropper's tombstone, the dropper's next login would
        // load it back into the backpack.
        var items = TestItems.Create();
        items.Add([_backpack, _bag, _coin, _dagger, _shirt, _ground]);
        items.PlaceOnGround(_dagger, MapType.Trammel, new Point3D(1496, 1629, 10));
        items.Release(_dagger, Aria);

        items.Absorb(_dagger);

        Assert.Contains(_dagger.Id, items.TombstonesOf(Aria));
        Assert.Empty(items.TakeReleasedOf(Aria));
    }

    [Fact]
    public void Decay_AnAbsorbedOrRemovedGroundItemStops()
    {
        var (items, _) = Decaying();

        items.Absorb(_ground);

        Assert.Null(_ground.DecayAt);
    }

    private (ItemService Items, SettableClock Clock) Decaying()
    {
        var clock = new SettableClock();
        var decay = new ItemDecayQueue(
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "item" })),
            new FakeTileDataService(),
            clock
        );
        var items = TestItems.Create(decay: decay);
        items.Add([_backpack, _bag, _coin, _dagger, _shirt, _ground]);

        return (items, clock);
    }

    private (ItemService Items, RecordingItemScriptService Scripts) Scripted()
    {
        var scripts = new RecordingItemScriptService();
        scripts.Scripted.Add("item");
        var items = TestItems.Create(scripts: scripts);
        items.Add([_backpack, _bag, _coin, _dagger, _shirt, _ground]);

        return (items, scripts);
    }

    private static ItemEntity Item(uint serial)
    {
        return new() { Id = new(serial), TemplateId = "item", ItemId = 0x0E75, Amount = 1 };
    }
}
