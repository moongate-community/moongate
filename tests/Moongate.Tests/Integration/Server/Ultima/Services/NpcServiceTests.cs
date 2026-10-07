using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
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
        Assert.Equal(
            [orc.Id],
            sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1497, 1628, 0), 0).Select(mobile => mobile.Id)
        );
        Assert.All([shirt.Id, backpack.Id, gold.Id], serial => Assert.True(items.TryGet(serial, out _)));
        Assert.False(mobiles.IsInWorld(aria.Id));
        Assert.False(items.TryGet(ariaPack.Id, out _));
    }

    [Fact]
    public async Task StartAsync_GivesTheNpcsSavedWithoutOneTheNotorietyOfTheirTemplate()
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
        var banker = new MobileEntity
        {
            Name = "Bob", TemplateId = "banker", Body = 0x0190, Map = MapType.Trammel, Location = new Point3D(1497, 1628, 0)
        };
        var cat = new MobileEntity
            { Name = "a cat", TemplateId = "cat", Body = 201, Map = MapType.Trammel, Location = new Point3D(1498, 1628, 0) };
        var priest = new MobileEntity
        {
            Name = "Fra", TemplateId = "priest", Body = 0x0190, Map = MapType.Trammel, Location = new Point3D(1499, 1628, 0)
        };
        var rat = new MobileEntity
        {
            Name = "a rat", TemplateId = "rat", Body = 238, Map = MapType.Trammel, Location = new Point3D(1500, 1628, 0),
            Notoriety = NotorietyType.Murderer
        };

        foreach (var npc in new[] { banker, cat, priest, rat })
        {
            await mobileData.UpsertAsync(npc);
        }

        await using var fixture = await SessionFixture.CreateAsync();
        var templates = new MobileTemplateService(
            new StubDataLoaderService().With(
                new MobileTemplate { Id = "banker", Notoriety = NotorietyType.Invulnerable },
                new MobileTemplate { Id = "cat" },
                new MobileTemplate { Id = "priest" },
                new MobileTemplate { Id = "rat", Notoriety = NotorietyType.Enemy }
            )
        );

        await new NpcService(
                new StubMobileFactoryService { Spawned = new SpawnedMobile(banker, [], null!, []) },
                mobiles,
                items,
                new RecordingWorldViewService(),
                mobileData,
                itemData,
                fixture.Loop,
                templates: templates
            )
            .StartAsync();

        Assert.True(mobiles.TryGet(banker.Id, out var loadedBanker));
        Assert.True(mobiles.TryGet(cat.Id, out var loadedCat));
        Assert.True(mobiles.TryGet(priest.Id, out var loadedPriest));
        Assert.True(mobiles.TryGet(rat.Id, out var loadedRat));
        Assert.Equal(NotorietyType.Invulnerable, loadedBanker.Notoriety);
        Assert.Equal(NotorietyType.Attackable, loadedCat.Notoriety);
        // A human with none stays as it is, and one that has its own keeps it.
        Assert.Null(loadedPriest.Notoriety);
        Assert.Equal(NotorietyType.Murderer, loadedRat.Notoriety);
    }

    private static ItemEntity Worn(MobileEntity mobile, string template, int graphic, LayerType layer)
    {
        var item = new ItemEntity { TemplateId = template, ItemId = graphic, Amount = 1 };
        item.Equip(mobile.Id, layer);

        return item;
    }
}
