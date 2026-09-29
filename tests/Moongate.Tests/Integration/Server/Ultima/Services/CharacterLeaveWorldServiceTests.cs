using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class CharacterLeaveWorldServiceTests
{
    [Fact]
    public async Task Leaving_AfterAMerge_TheAbsorbedStackIsGoneAndTheOtherHoldsTheSum()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(mobiles);
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        host.Container.RegisterMoongateEventBus();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobileData = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var itemData = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobileData.UpsertAsync(aria);
        var backpack = new ItemEntity { TemplateId = "backpack", ItemId = 0x0E75 };
        backpack.Equip(aria.Id, LayerType.Backpack);
        await itemData.UpsertAsync(backpack);
        var pile = Gold(backpack.Id, 70);
        var coins = Gold(backpack.Id, 30);
        await itemData.UpsertAsync(pile);
        await itemData.UpsertAsync(coins);
        mobiles.EnterWorld(aria);
        items.Add([backpack, pile, coins]);
        pile.Amount += coins.Amount;
        items.Absorb(coins);
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, aria.Id));
        var service = new CharacterLeaveWorldService(
            mobiles,
            items,
            new RecordingWorldViewService(),
            new WorldTransactionService(host.Owner),
            host.Container.Resolve<IMoongateEventBus>()
        );

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(await itemData.GetByIdAsync(coins.Id));
        Assert.Equal(100, (await itemData.GetByIdAsync(pile.Id))!.Amount);
        Assert.Empty(items.TombstonesOf(aria.Id));
    }

    [Fact]
    public async Task Leaving_AfterASwapOnTheSameLayer_SavesTheNewOutfit()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(mobiles);
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        host.Container.RegisterMoongateEventBus();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobileData = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var itemData = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobileData.UpsertAsync(aria);
        var backpack = new ItemEntity { Id = new(0x40001000), TemplateId = "backpack", ItemId = 0x0E75 };
        backpack.Equip(aria.Id, LayerType.Backpack);
        // The new shirt's serial is the lower one, so it would be written first.
        var newShirt = new ItemEntity { Id = new(0x40000001), TemplateId = "shirt", ItemId = 0x1517 };
        newShirt.PutInContainer(backpack.Id, new Point2D(44, 65));
        var oldShirt = new ItemEntity { Id = new(0x40002000), TemplateId = "shirt", ItemId = 0x1517 };
        oldShirt.Equip(aria.Id, LayerType.Shirt);
        await itemData.UpsertAsync(backpack);
        await itemData.UpsertAsync(newShirt);
        await itemData.UpsertAsync(oldShirt);
        mobiles.EnterWorld(aria);
        items.Add([backpack, newShirt, oldShirt]);
        items.MoveToContainer(oldShirt, backpack.Id, new Point2D(60, 70));
        items.Equip(newShirt, aria.Id, LayerType.Shirt);
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, aria.Id));
        var service = new CharacterLeaveWorldService(
            mobiles,
            items,
            new RecordingWorldViewService(),
            new WorldTransactionService(host.Owner),
            host.Container.Resolve<IMoongateEventBus>()
        );

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(LayerType.Shirt, (await itemData.GetByIdAsync(newShirt.Id))!.Layer);
        Assert.Equal(backpack.Id, (await itemData.GetByIdAsync(oldShirt.Id))!.ContainerId);
    }

    [Fact]
    public async Task Leaving_AfterAWornItemWasAbsorbed_FreesItsLayer()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(mobiles);
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        host.Container.RegisterMoongateEventBus();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobileData = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var itemData = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobileData.UpsertAsync(aria);
        var backpack = new ItemEntity { Id = new(0x40001000), TemplateId = "backpack", ItemId = 0x0E75 };
        backpack.Equip(aria.Id, LayerType.Backpack);
        // The new shirt's serial is the lower one, so it would be written first.
        var newShirt = new ItemEntity { Id = new(0x40000001), TemplateId = "shirt", ItemId = 0x1517 };
        newShirt.PutInContainer(backpack.Id, new Point2D(44, 65));
        var oldShirt = new ItemEntity { Id = new(0x40002000), TemplateId = "shirt", ItemId = 0x1517 };
        oldShirt.Equip(aria.Id, LayerType.Shirt);
        await itemData.UpsertAsync(backpack);
        await itemData.UpsertAsync(newShirt);
        await itemData.UpsertAsync(oldShirt);
        mobiles.EnterWorld(aria);
        items.Add([backpack, newShirt, oldShirt]);
        // The old shirt is merged away: the leave deletes its row, which must happen before the new one is written.
        items.Absorb(oldShirt, aria.Id);
        items.Equip(newShirt, aria.Id, LayerType.Shirt);
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, aria.Id));
        var service = new CharacterLeaveWorldService(
            mobiles,
            items,
            new RecordingWorldViewService(),
            new WorldTransactionService(host.Owner),
            host.Container.Resolve<IMoongateEventBus>()
        );

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(LayerType.Shirt, (await itemData.GetByIdAsync(newShirt.Id))!.Layer);
        Assert.Null(await itemData.GetByIdAsync(oldShirt.Id));
    }

    [Fact]
    public async Task Leaving_DoesNotWriteADroppedItemSomeoneElseNowWears()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var mobiles = new MobileService(new StubMovementService(), TestSectors.Create());
        var items = TestItems.Create();
        host.Container.RegisterInstance<IMobileService>(mobiles);
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        host.Container.RegisterMoongateEventBus();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobileData = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var itemData = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        var bran = new MobileEntity { Name = "Bran", AccountId = new Serial(0x43), Slot = 0, Map = MapType.Trammel };
        await mobileData.UpsertAsync(aria);
        await mobileData.UpsertAsync(bran);
        var ariasBackpack = new ItemEntity { TemplateId = "backpack", ItemId = 0x0E75 };
        ariasBackpack.Equip(aria.Id, LayerType.Backpack);
        var ariasSword = new ItemEntity { TemplateId = "katana", ItemId = 0x13FF };
        ariasSword.Equip(aria.Id, LayerType.OneHanded);
        var dropped = new ItemEntity { TemplateId = "katana", ItemId = 0x13FF };
        dropped.PlaceOnGround(MapType.Trammel, new Point3D(1, 1, 0));
        await itemData.UpsertAsync(ariasBackpack);
        await itemData.UpsertAsync(ariasSword);
        await itemData.UpsertAsync(dropped);
        mobiles.EnterWorld(aria);
        mobiles.EnterWorld(bran);
        items.Add([ariasBackpack, ariasSword, dropped]);
        // Bran dropped it; Aria takes her own sword off (in memory only) and wears his.
        items.Release(dropped, bran.Id);
        items.MoveToContainer(ariasSword, ariasBackpack.Id, new Point2D(44, 65));
        items.Equip(dropped, aria.Id, LayerType.OneHanded);
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        await fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, bran.Id));
        bran.Location = new Point3D(5, 5, 0);
        var service = new CharacterLeaveWorldService(
            mobiles,
            items,
            new RecordingWorldViewService(),
            new WorldTransactionService(host.Owner),
            host.Container.Resolve<IMoongateEventBus>()
        );

        await fixture.ExecuteOnLoopAsync(() => service.OnSessionClosed(session));
        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        // Bran's leave saved, without writing Aria's sword: her own leave or the world save does.
        Assert.Equal(new Point3D(5, 5, 0), (await mobileData.GetByIdAsync(bran.Id))!.Location);
        Assert.NotNull((await itemData.GetByIdAsync(dropped.Id))!.GroundLocation);
        Assert.Equal(aria.Id, (await itemData.GetByIdAsync(ariasSword.Id))!.MobileId);
    }

    private static ItemEntity Gold(Serial container, int amount)
    {
        var gold = new ItemEntity { TemplateId = "gold", ItemId = 0x0EED, Amount = amount };
        gold.PutInContainer(container, new Point2D(44, 65));

        return gold;
    }
}
