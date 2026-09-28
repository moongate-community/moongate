using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Services;

[Collection(PostgresTestCollection.Name)]
public sealed class GroundItemsLoadServiceTests
{
    [Fact]
    public async Task StartAsync_LoadsGroundItemsAndTheirContentsIntoTheWorld()
    {
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        var sectors = TestSectors.Create();
        var live = TestItems.Create(sectors);
        host.Container.RegisterInstance<IMobileService>(new MobileService(new StubMovementService(), sectors));
        host.Container.RegisterInstance<IItemService>(live);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        var data = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var bag = new ItemEntity { TemplateId = "bag", ItemId = 0x0E76 };
        bag.PlaceOnGround(MapType.Trammel, new Point3D(1497, 1628, 0));
        await data.UpsertAsync(bag);
        var coins = new ItemEntity { TemplateId = "gold", ItemId = 0x0EED, Amount = 5 };
        coins.PutInContainer(bag.Id, new Point2D(44, 65));
        await data.UpsertAsync(coins);
        await using var fixture = await SessionFixture.CreateAsync();

        await new GroundItemsLoadService(data, live, fixture.Loop).StartAsync();

        Assert.True(live.TryGet(bag.Id, out _));
        Assert.True(live.TryGet(coins.Id, out _));
        Assert.Equal([bag.Id], sectors.GetItemsInRange(MapType.Trammel, new Point3D(1497, 1628, 0), 0).Select(item => item.Id));
    }
}
