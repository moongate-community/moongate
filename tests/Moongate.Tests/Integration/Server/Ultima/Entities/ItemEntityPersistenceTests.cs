using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Persistence;
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
    public async Task EveryLocation_AndProps_RoundTrip()
    {
        var mobile = await NewMobileAsync();
        var backpack = Item(0x40000001, i => i.Equip(mobile.Id, LayerType.Backpack));
        var coin = Item(0x40000002, i => i.PutInContainer(backpack.Id, 44, 65));
        var wand = Item(0x40000003, i => i.PlaceOnGround(MapType.Trammel, new Point3D(1602, 1591, 20)));
        wand.Props = new ItemProps { Charges = 12, Tags = new() { ["quest_step"] = "3" } };
        coin.Amount = 250;

        foreach (var item in new[] { backpack, coin, wand })
        {
            await _items.UpsertAsync(item);
        }

        var loadedCoin = (await _items.GetByIdAsync(coin.Id))!;
        var loadedWand = (await _items.GetByIdAsync(wand.Id))!;
        var loadedPack = (await _items.GetByIdAsync(backpack.Id))!;

        Assert.Equal((backpack.Id, (short)44, (short)65, 250), (loadedCoin.ContainerId!.Value, loadedCoin.GridX!.Value, loadedCoin.GridY!.Value, loadedCoin.Amount));
        Assert.Equal((MapType.Trammel, new Point3D(1602, 1591, 20)), (loadedWand.GroundMap!.Value, loadedWand.GroundLocation!.Value));
        Assert.Equal(12, loadedWand.Props!.Charges);
        Assert.Equal("3", loadedWand.Props.Tags!["quest_step"]);
        Assert.Equal((mobile.Id, LayerType.Backpack), (loadedPack.MobileId!.Value, loadedPack.WornLayer!.Value));
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
        item.Layer = (byte)LayerType.Helm;

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
        var bag = Item(0x40000031, i => i.PutInContainer(chest.Id, 1, 1));
        var gem = Item(0x40000032, i => i.PutInContainer(bag.Id, 2, 2));

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
        var coin = Item(0x40000041, i => i.PutInContainer(backpack.Id, 1, 1));

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
                await items.UpsertAsync(Item(0x40000051, i => i.PutInContainer(new Serial(0x40000050), 1, 1)));
            }
        );

        Assert.NotNull(await _items.GetByIdAsync(new Serial(0x40000051)));
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
}
