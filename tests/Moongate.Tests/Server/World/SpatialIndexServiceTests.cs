using Moongate.Core.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.World;

public class SpatialIndexServiceTests
{
    [Fact]
    public void AddOrUpdate_AfterMove_RelocatesAcrossSectors()
    {
        var (index, persistence) = Build();
        var mobile = SeedMobile(persistence, 0x1, 0, 15, 0);
        index.AddOrUpdate(mobile);

        // Cross the sector boundary (tile 15 -> sector 0, tile 400 -> sector 25) and re-index.
        mobile.Position = new(400, 400, 0);
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
        index.AddOrUpdate(mobile);

        Assert.Empty(index.GetMobilesInRange(0, new(15, 0, 0), 10));
        Assert.Single(index.GetMobilesInRange(0, new(400, 400, 0), 10));
    }

    [Fact]
    public void AddOrUpdate_ItemInContainer_ActsAsRemove()
    {
        var (index, persistence) = Build();
        var item = SeedItem(persistence, 0x40000001, 0, 50, 50);
        index.AddOrUpdate(item);

        item.ParentContainerId = new(0x40000099);
        persistence.Store<ItemEntity>().UpsertAsync(item).WaitSync();
        index.AddOrUpdate(item);

        Assert.Empty(index.GetItemsInRange(0, new(50, 50, 0), 10));
    }

    [Fact]
    public void AddOrUpdate_SameSectorTwice_DoesNotDuplicate()
    {
        var (index, persistence) = Build();
        var mobile = SeedMobile(persistence, 0x1, 0, 100, 100);

        index.AddOrUpdate(mobile);
        index.AddOrUpdate(mobile);

        Assert.Single(index.GetMobilesInRange(0, new(100, 100, 0), 5));
    }

    [Fact]
    public void AddOrUpdate_ThenQueryInRange_ReturnsMobile()
    {
        var (index, persistence) = Build();
        var mobile = SeedMobile(persistence, 0x1, 0, 100, 100);

        index.AddOrUpdate(mobile);
        var found = index.GetMobilesInRange(0, new(105, 105, 0), 18);

        Assert.Single(found);
        Assert.Equal(mobile.Id, found[0].Id);
    }

    [Fact]
    public void GetItemsInRange_ReturnsGroundItem()
    {
        var (index, persistence) = Build();
        index.AddOrUpdate(SeedItem(persistence, 0x40000001, 0, 50, 50));

        Assert.Single(index.GetItemsInRange(0, new(52, 52, 0), 10));
    }

    [Fact]
    public void GetMobilesInRange_OutOfRange_ReturnsEmpty()
    {
        var (index, persistence) = Build();
        index.AddOrUpdate(SeedMobile(persistence, 0x1, 0, 100, 100));

        Assert.Empty(index.GetMobilesInRange(0, new(300, 300, 0), 18));
    }

    [Fact]
    public void Query_IgnoresOtherMaps()
    {
        var (index, persistence) = Build();
        index.AddOrUpdate(SeedMobile(persistence, 0x1, 1, 100, 100));

        Assert.Empty(index.GetMobilesInRange(0, new(100, 100, 0), 18));
        Assert.Single(index.GetMobilesInRange(1, new(100, 100, 0), 18));
    }

    [Fact]
    public void Query_SkipsEntitiesDeletedFromStore()
    {
        var (index, persistence) = Build();
        var mobile = SeedMobile(persistence, 0x1, 0, 100, 100);
        index.AddOrUpdate(mobile);

        persistence.Store<MobileEntity>().RemoveAsync(mobile.Id).WaitSync();

        Assert.Empty(index.GetMobilesInRange(0, new(100, 100, 0), 18));
    }

    [Fact]
    public void Query_SpanningMultipleSectors_ReturnsAllWithoutDuplicates()
    {
        var (index, persistence) = Build();
        index.AddOrUpdate(SeedMobile(persistence, 0x1, 0, 100, 100));
        index.AddOrUpdate(SeedMobile(persistence, 0x2, 0, 118, 118));

        var found = index.GetMobilesInRange(0, new(109, 109, 0), 20);

        Assert.Equal(2, found.Count);
        Assert.Equal(2, found.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public void Remove_ThenQuery_ReturnsEmpty()
    {
        var (index, persistence) = Build();
        var mobile = SeedMobile(persistence, 0x1, 0, 100, 100);
        index.AddOrUpdate(mobile);

        index.Remove(mobile.Id);

        Assert.Empty(index.GetMobilesInRange(0, new(100, 100, 0), 18));
    }

    [Fact]
    public void Remove_UnknownSerial_IsNoOp()
    {
        var (index, _) = Build();

        index.Remove(new(0xDEAD));
    }

    private static (SpatialIndexService Index, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();
        var marker = new LoopThreadMarker();
        marker.Capture();

        return (new(persistence, marker), persistence);
    }

    private static ItemEntity SeedItem(FakePersistenceService persistence, uint serial, int mapId, int x, int y)
    {
        var item = new ItemEntity { Id = new(serial), MapId = mapId, Position = new(x, y, 0) };
        persistence.Store<ItemEntity>().UpsertAsync(item).WaitSync();

        return item;
    }

    private static MobileEntity SeedMobile(FakePersistenceService persistence, uint serial, int mapId, int x, int y)
    {
        var mobile = new MobileEntity { Id = new(serial), MapId = mapId, Position = new(x, y, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        return mobile;
    }
}
