using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;
using Npgsql;

namespace Moongate.Tests.Integration.Server.Ultima.Entities;

[Collection(PostgresTestCollection.Name)]
public sealed class ItemEntityPersistenceTests : IAsyncLifetime
{
    private HostPersistenceFixture _host = null!;
    private IDataAccess<MobileEntity> _mobiles = null!;
    private IDataAccess<ItemEntity> _items = null!;

    public async Task InitializeAsync()
    {
        _host = await HostPersistenceFixture.CreateAsync(false);
        _host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>();
        await CoreMigrationFiles.ApplyAsync(_host.Database, "world");
        await _host.Owner.InitializeAsync();
        _mobiles = _host.Container.Resolve<IDataAccess<MobileEntity>>();
        _items = _host.Container.Resolve<IDataAccess<ItemEntity>>();
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task ASchemaGeneratedFromTheEntities_GivesTheFirstItemTheFirstItemSerial()
    {
        // A development root with no reviewed migrations gets its schema generated from the entities; the item
        // sequence must still start in the item range, not at 1.
        await using var host = await HostPersistenceFixture.CreateAsync();
        host.Container.AddPersistenceWorld<ItemEntity>();
        await host.Owner.InitializeAsync();
        var items = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var item = new ItemEntity { TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        item.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0));

        await items.UpsertAsync(item);

        Assert.Equal(new Serial(Serial.MinItem), item.Id);
    }

    [Fact]
    public async Task EveryLocation_AndProps_RoundTrip()
    {
        var mobile = await NewMobileAsync();
        var backpack = Item(0x40000001, i => i.Equip(mobile.Id, LayerType.Backpack));
        var coin = Item(0x40000002, i => i.PutInContainer(backpack.Id, new Point2D(44, 65), 9));
        var wand = Item(0x40000003, i => i.PlaceOnGround(MapType.Trammel, new Point3D(1602, 1591, 20)));
        wand.SetProp(ItemPropKeys.Charges, 12);
        wand.SetProp(ItemPropKeys.Quality, ItemQualityType.Exceptional);
        wand.SetProp("quest_step", "3");
        wand.Visibility = AccountType.GameMaster;
        coin.Amount = 250;

        foreach (var item in new[] { backpack, coin, wand })
        {
            await _items.UpsertAsync(item);
        }

        var loadedCoin = (await _items.GetByIdAsync(coin.Id))!;
        var loadedWand = (await _items.GetByIdAsync(wand.Id))!;
        var loadedPack = (await _items.GetByIdAsync(backpack.Id))!;

        Assert.Equal((backpack.Id, new Point2D(44, 65), 250), (loadedCoin.ContainerId!.Value, loadedCoin.GridLocation!.Value, loadedCoin.Amount));
        Assert.Equal(((short?)9, (short?)null, (short?)null), (loadedCoin.GridIndex, loadedWand.GridIndex, loadedPack.GridIndex));
        Assert.Equal((MapType.Trammel, new Point3D(1602, 1591, 20)), (loadedWand.Map!.Value, loadedWand.GroundLocation!.Value));
        Assert.Equal(12, loadedWand.GetProp<int>(ItemPropKeys.Charges));
        Assert.Equal(ItemQualityType.Exceptional, loadedWand.GetProp(ItemPropKeys.Quality, ItemQualityType.Regular));
        Assert.Equal(AccountType.GameMaster, loadedWand.Visibility);
        Assert.Null(loadedCoin.Visibility);
        Assert.Equal("3", loadedWand.GetProp<string>("quest_step"));
        Assert.Equal((mobile.Id, LayerType.Backpack), (loadedPack.MobileId!.Value, loadedPack.Layer!.Value));
    }

    [Fact]
    public async Task ATimerOfAnItem_ComesBackFromTheDatabase_AndIsQueuedAgain()
    {
        var clock = new SettableClock();
        var door = Item(0x40000040, i => i.PlaceOnGround(MapType.Felucca, new Point3D(5, 5, 0)));
        var started = new ItemTimerQueue(clock);
        var timers = new ItemTimerService(new RecordingTimerService(), started, TestItems.Create(), new RecordingItemScriptService(), clock);
        Assert.True(timers.Start(door, "close", TimeSpan.FromSeconds(20)));
        await _items.UpsertAsync(door);

        var loaded = (await _items.GetByIdAsync(door.Id))!;
        var queue = new ItemTimerQueue(clock);
        queue.Track(loaded);
        clock.Advance(TimeSpan.FromSeconds(20));

        var entry = Assert.Single(queue.TakeDue());
        Assert.Equal((door.Id, "close", door.GetProp<long>("timer.close")), (entry.Item, entry.Name, entry.DueAt));
        Assert.Equal(TimeSpan.Zero, timers.Remaining(loaded, "close"));
    }

    [Fact]
    public async Task AnItemNowhere_IsRejectedByTheDatabase()
    {
        await AssertRejectedAsync(new ItemEntity { Id = new(0x40000010), TemplateId = "t", ItemId = 1 });
    }

    [Fact]
    public async Task AnItemInTwoPlaces_IsRejectedByTheDatabase()
    {
        var mobile = await NewMobileAsync();
        var item = Item(0x40000011, i => i.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0)));
        item.MobileId = mobile.Id;
        item.Layer = LayerType.Helm;

        await AssertRejectedAsync(item);
    }

    [Fact]
    public async Task SelfContainedItem_IsRejectedByTheDatabase()
    {
        var item = new ItemEntity { Id = new(0x40000012), TemplateId = "t", ItemId = 1, ContainerId = new(0x40000012), GridX = 0, GridY = 0 };

        await AssertRejectedAsync(item);
    }

    [Fact]
    public async Task AnIdOutsideTheItemRange_IsRejectedByTheDatabase()
    {
        await AssertRejectedAsync(Item(0x00000500, i => i.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0))));
    }

    [Fact]
    public async Task TwoItemsOnTheSameLayer_AreRejected()
    {
        var mobile = await NewMobileAsync();
        await _items.UpsertAsync(Item(0x40000020, i => i.Equip(mobile.Id, LayerType.Helm)));

        await AssertRejectedAsync(Item(0x40000021, i => i.Equip(mobile.Id, LayerType.Helm)));
    }

    [Fact]
    public async Task DeletingAContainer_DeletesItsContentsRecursively()
    {
        var chest = Item(0x40000030, i => i.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0)));
        var bag = Item(0x40000031, i => i.PutInContainer(chest.Id, new Point2D(1, 1)));
        var gem = Item(0x40000032, i => i.PutInContainer(bag.Id, new Point2D(2, 2)));

        foreach (var item in new[] { chest, bag, gem })
        {
            await _items.UpsertAsync(item);
        }

        await _items.DeleteAsync(chest.Id);

        Assert.Null(await _items.GetByIdAsync(bag.Id));
        Assert.Null(await _items.GetByIdAsync(gem.Id));
    }

    [Fact]
    public async Task DeletingAMobile_DeletesItsEquipmentAndBackpackContents()
    {
        var mobile = await NewMobileAsync();
        var backpack = Item(0x40000040, i => i.Equip(mobile.Id, LayerType.Backpack));
        var coin = Item(0x40000041, i => i.PutInContainer(backpack.Id, new Point2D(1, 1)));

        await _items.UpsertAsync(backpack);
        await _items.UpsertAsync(coin);
        await _mobiles.DeleteAsync(mobile.Id);

        Assert.Null(await _items.GetByIdAsync(backpack.Id));
        Assert.Null(await _items.GetByIdAsync(coin.Id));
    }

    [Fact]
    public async Task UpsertAsync_NewItem_GetsAnIdInTheItemRange()
    {
        var item = Item(0, i => i.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0)));

        await _items.UpsertAsync(item);

        Assert.InRange(item.Id.Value, Serial.MinItem, Serial.MaxItem);
    }

    [Fact]
    public async Task ANewContainerWithNewContents_SavesInOneTransactionParentFirst()
    {
        await _host.Owner.ExecuteInTransactionAsync(
            PersistenceDatabaseTarget.Realm,
            async transaction =>
            {
                var items = transaction.GetDataAccess<ItemEntity>();
                await items.UpsertAsync(Item(0x40000050, i => i.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0))));
                await items.UpsertAsync(Item(0x40000051, i => i.PutInContainer(new Serial(0x40000050), new Point2D(1, 1))));
            }
        );

        Assert.NotNull(await _items.GetByIdAsync(new Serial(0x40000051)));
    }

    [Fact]
    public async Task AnItemWithoutATemplate_IsRejectedByTheDatabase()
    {
        var exception = await Record.ExceptionAsync(() => _host.Database.ExecuteAsync(
            "INSERT INTO world.items (id, template_id, item_id, hue, amount, map, x, y, z) " +
            "VALUES (1073741900, NULL, 1, 0, 1, 0, 1, 1, 0)"
        ));

        Assert.IsType<PostgresException>(exception);
    }

    [Fact]
    public async Task RarityAndLootType_RoundTrip()
    {
        var item = Item(0x40000060, i => i.PlaceOnGround(MapType.Felucca, new Point3D(1, 1, 0)));
        item.Rarity = ItemRarityType.Epic;
        item.SetProp(ItemPropKeys.LootType, LootType.Newbied);

        await _items.UpsertAsync(item);
        var loaded = (await _items.GetByIdAsync(item.Id))!;

        Assert.Equal((ItemRarityType.Epic, (LootType?)LootType.Newbied), (loaded.Rarity, (LootType?)loaded.GetProp<LootType>(ItemPropKeys.LootType)));
    }

    [Fact]
    public async Task ARarityOutsideTheEnum_IsRejectedByTheDatabase()
    {
        var exception = await Record.ExceptionAsync(() => _host.Database.ExecuteAsync(
            "INSERT INTO world.items (id, template_id, item_id, hue, amount, rarity, map, x, y, z) " +
            "VALUES (1073741901, 't', 1, 0, 1, 9, 0, 1, 1, 0)"
        ));

        Assert.IsType<PostgresException>(exception);
    }

    private async Task<MobileEntity> NewMobileAsync()
    {
        var mobile = new MobileEntity { Id = new(0x00000100 + (uint)Random.Shared.Next(1, 100000)), Name = "Aria" };
        await _mobiles.UpsertAsync(mobile);

        return mobile;
    }

    private static ItemEntity Item(uint id, Action<ItemEntity> place)
    {
        var item = new ItemEntity { Id = new(id), TemplateId = "test", ItemId = 0x0E75 };
        place(item);

        return item;
    }

    private async Task AssertRejectedAsync(ItemEntity item)
    {
        var exception = await Record.ExceptionAsync(() => _items.UpsertAsync(item));

        Assert.NotNull(exception);
        Assert.True(
            exception is PostgresException || exception.InnerException is PostgresException,
            exception.ToString()
        );
    }

    [Fact]
    public async Task WorldSave_WritesTheLiveItemsAsTheyAreNow()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(new MobileService(new StubMovementService(), TestSectors.Create()));
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var data = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobiles.UpsertAsync(aria);
        var backpack = new ItemEntity { TemplateId = "backpack", ItemId = 0x0E75 };
        backpack.Equip(aria.Id, LayerType.Backpack);
        await data.UpsertAsync(backpack);
        var coins = new ItemEntity { TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        coins.PutInContainer(backpack.Id, new Point2D(44, 65));
        await data.UpsertAsync(coins);
        items.Add([backpack, coins]);
        coins.Amount = 7;
        coins.PutInContainer(backpack.Id, new Point2D(90, 90));

        await host.Owner.SaveAllAsync();

        var stored = (await data.GetByIdAsync(coins.Id))!;
        Assert.Equal((7, new Point2D(90, 90)), (stored.Amount, stored.GridLocation!.Value));
    }

    [Theory, InlineData(0x40000000u), InlineData(0x40003000u)]
    public async Task WorldSave_ASwapOnTheSameLayer_KeepsTheNewOutfit(uint newShirtSerial)
    {
        // The new shirt's serial is below or above the old one's, so either write order is exercised.
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(new MobileService(new StubMovementService(), TestSectors.Create()));
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var data = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobiles.UpsertAsync(aria);
        var backpack = new ItemEntity { Id = new(0x40001000), TemplateId = "backpack", ItemId = 0x0E75 };
        backpack.Equip(aria.Id, LayerType.Backpack);
        var oldShirt = new ItemEntity { Id = new(0x40002000), TemplateId = "shirt", ItemId = 0x1517 };
        oldShirt.Equip(aria.Id, LayerType.Shirt);
        var newShirt = new ItemEntity { Id = new(newShirtSerial + 1), TemplateId = "shirt", ItemId = 0x1517 };
        newShirt.PutInContainer(backpack.Id, new Point2D(44, 65));
        await data.UpsertAsync(backpack);
        await data.UpsertAsync(oldShirt);
        await data.UpsertAsync(newShirt);
        items.Add([backpack, oldShirt, newShirt]);

        items.MoveToContainer(oldShirt, backpack.Id, new Point2D(60, 70));
        items.Equip(newShirt, aria.Id, LayerType.Shirt);
        await host.Owner.SaveAllAsync();

        var storedNew = (await data.GetByIdAsync(newShirt.Id))!;
        var storedOld = (await data.GetByIdAsync(oldShirt.Id))!;
        Assert.Equal((aria.Id, LayerType.Shirt), (storedNew.MobileId!.Value, storedNew.Layer!.Value));
        Assert.Equal(backpack.Id, storedOld.ContainerId);
        Assert.Null(storedOld.MobileId);
    }

    [Fact]
    public async Task WorldSave_AnAbsorbedWornItem_FreesItsLayerForTheNextOne()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(new MobileService(new StubMovementService(), TestSectors.Create()));
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobiles = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var data = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobiles.UpsertAsync(aria);
        var backpack = new ItemEntity { TemplateId = "backpack", ItemId = 0x0E75 };
        backpack.Equip(aria.Id, LayerType.Backpack);
        var worn = new ItemEntity { TemplateId = "torch", ItemId = 0x0F64 };
        worn.Equip(aria.Id, LayerType.TwoHanded);
        var next = new ItemEntity { TemplateId = "torch", ItemId = 0x0F64 };
        await data.UpsertAsync(backpack);
        await data.UpsertAsync(worn);
        next.PutInContainer(backpack.Id, new Point2D(44, 65));
        await data.UpsertAsync(next);
        items.Add([backpack, worn, next]);

        // The worn torch is merged into a stack (its row is deleted by the save), and another goes on.
        items.Absorb(worn, aria.Id);
        items.Equip(next, aria.Id, LayerType.TwoHanded);
        await host.Owner.SaveAllAsync();

        Assert.Null(await data.GetByIdAsync(worn.Id));
        Assert.Equal(LayerType.TwoHanded, (await data.GetByIdAsync(next.Id))!.Layer);
    }

    [Fact]
    public async Task WorldSave_DeletesTheItemsAbsorbedIntoOtherStacks()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(new MobileService(new StubMovementService(), TestSectors.Create()));
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var data = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var gold = new ItemEntity { TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        gold.PlaceOnGround(MapType.Trammel, new Point3D(1, 1, 0));
        await data.UpsertAsync(gold);
        items.Add([gold]);

        items.Absorb(gold);
        await host.Owner.SaveAllAsync();

        Assert.Null(await data.GetByIdAsync(gold.Id));
        Assert.Empty(((IPersistenceDeletionSource)items).Capture());
    }
}
