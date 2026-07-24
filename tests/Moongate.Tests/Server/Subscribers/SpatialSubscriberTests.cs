using Moongate.Core.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Subscribers;

public class SpatialSubscriberTests
{
    [Fact]
    public async Task OnPlayerEnteredWorld_IndexesTheMobile()
    {
        var (subscriber, spatial, persistence) = Build();
        var character = new MobileEntity { Id = new(0x20), MapId = 0, Position = new(200, 200, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(character).WaitSync();

        await subscriber.OnPlayerEnteredWorld(new(1, new(0x999), character), CancellationToken.None);

        Assert.Single(spatial.GetMobilesInRange(0, new(200, 200, 0), 5));
    }

    [Fact]
    public async Task OnWorldReady_IndexesNpcsAndGroundItems_SkipsCharactersAndContainedItems()
    {
        var (subscriber, spatial, persistence) = Build();

        // An NPC and a player character at the same spot: only the NPC must be indexed.
        var npc = new MobileEntity { Id = new(0x10), MapId = 0, Position = new(100, 100, 0) };
        var character = new MobileEntity { Id = new(0x20), MapId = 0, Position = new(100, 100, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(npc).WaitSync();
        persistence.Store<MobileEntity>().UpsertAsync(character).WaitSync();
        persistence.Store<AccountEntity>()
            .UpsertAsync(new() { Id = new(0x999), Username = "bob", MobileIds = [character.Id] })
            .WaitSync();

        // A ground item and a contained item: only the ground one must be indexed.
        var ground = new ItemEntity { Id = new(0x40000001), MapId = 0, Position = new(100, 100, 0) };
        var contained = new ItemEntity
        {
            Id = new(0x40000002), MapId = 0, Position = new(100, 100, 0), ParentContainerId = new(0x40000099)
        };
        persistence.Store<ItemEntity>().UpsertAsync(ground).WaitSync();
        persistence.Store<ItemEntity>().UpsertAsync(contained).WaitSync();

        await subscriber.OnWorldReady(new(), CancellationToken.None);

        var mobiles = spatial.GetMobilesInRange(0, new(100, 100, 0), 5);
        Assert.Single(mobiles);
        Assert.Equal(npc.Id, mobiles[0].Id);
        var items = spatial.GetItemsInRange(0, new(100, 100, 0), 5);
        Assert.Single(items);
        Assert.Equal(ground.Id, items[0].Id);
    }

    private static (SpatialSubscriber Subscriber, SpatialIndexService Spatial, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), new StubEventBus());

        return (new(spatial, persistence), spatial, persistence);
    }
}
