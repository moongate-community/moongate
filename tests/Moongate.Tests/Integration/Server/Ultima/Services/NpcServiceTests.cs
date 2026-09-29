using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class NpcServiceTests
{
    [Fact]
    public async Task StartAsync_LoadsTheNpcsAndTheirItemsButNotThePlayers()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var sectors = TestSectors.Create();
        var mobiles = new MobileService(new StubMovementService(), sectors);
        var items = TestItems.Create(sectors);
        host.Container.RegisterInstance<IMobileService>(mobiles);
        host.Container.RegisterInstance<IItemService>(items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var mobileData = host.Container.Resolve<IDataAccess<MobileEntity>>();
        var itemData = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var aria = new MobileEntity { Name = "Aria", AccountId = new Serial(0x42), Slot = 0, Map = MapType.Trammel };
        await mobileData.UpsertAsync(aria);
        var ariaPack = Worn(aria, "backpack", 0x0E75, LayerType.Backpack);
        await itemData.UpsertAsync(ariaPack);
        var orc = new MobileEntity
        {
            Name = "Orc", TemplateId = "orc", Body = 0x0011, Map = MapType.Trammel, Location = new Point3D(1497, 1628, 0)
        };
        await mobileData.UpsertAsync(orc);
        var shirt = Worn(orc, "shirt", 0x1517, LayerType.Shirt);
        var backpack = Worn(orc, "backpack", 0x0E75, LayerType.Backpack);
        await itemData.UpsertAsync(shirt);
        await itemData.UpsertAsync(backpack);
        var gold = new ItemEntity { TemplateId = "gold", ItemId = 0x0EED, Amount = 50 };
        gold.PutInContainer(backpack.Id, new Point2D(44, 65));
        await itemData.UpsertAsync(gold);
        await using var fixture = await SessionFixture.CreateAsync();
        var factory = new StubMobileFactoryService { Spawned = new SpawnedMobile(orc, [], backpack, []) };

        await new NpcService(factory, mobiles, items, new RecordingWorldViewService(), mobileData, itemData, fixture.Loop)
            .StartAsync();

        Assert.True(mobiles.IsInWorld(orc.Id));
        Assert.Equal([orc.Id], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1497, 1628, 0), 0).Select(mobile => mobile.Id));
        Assert.All([shirt.Id, backpack.Id, gold.Id], serial => Assert.True(items.TryGet(serial, out _)));
        Assert.False(mobiles.IsInWorld(aria.Id));
        Assert.False(items.TryGet(ariaPack.Id, out _));
    }

    private static ItemEntity Worn(MobileEntity mobile, string template, int graphic, LayerType layer)
    {
        var item = new ItemEntity { TemplateId = template, ItemId = graphic, Amount = 1 };
        item.Equip(mobile.Id, layer);

        return item;
    }
}
