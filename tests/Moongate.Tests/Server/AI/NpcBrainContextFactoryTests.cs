using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Services.AI;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.AI;

public class NpcBrainContextFactoryTests
{
    [Fact]
    public async Task Create_WorldState_ProducesDeterministicDetachedSnapshot()
    {
        var persistence = new FakePersistenceService();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), new StubEventBus());
        var sessions = new StubSessionManager();
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 24, 10, 30, 0, TimeSpan.Zero));
        var owner = await AddMobileAsync(persistence, spatial, 0x5, "owner", 0, 10, 10);
        var laterSerial = await AddMobileAsync(persistence, spatial, 0x9, "later", 0, 12, 10);
        var earlierSerial = await AddMobileAsync(persistence, spatial, 0x2, "earlier", 0, 11, 10);
        await AddMobileAsync(persistence, spatial, 0x3, "outside", 0, 20, 10);
        sessions.Played.Add(laterSerial.Id);
        var factory = new NpcBrainContextFactory(spatial, sessions, time);
        var homePosition = new Point3D(4, 5, 6);

        var context = factory.Create(owner, 1, homePosition, new BrainDescriptor("guard", 1000, 3, 15));

        Assert.Equal(time.Now, context.Now);
        Assert.Equal(owner.Id, context.Self.Id);
        Assert.Equal("owner", context.Self.Name);
        Assert.False(context.Self.IsPlayer);
        Assert.Equal(1, context.HomeMapId);
        Assert.Equal(homePosition, context.HomePosition);
        Assert.Equal([earlierSerial.Id, laterSerial.Id], context.Nearby.Select(snapshot => snapshot.Id));
        Assert.False(context.Nearby[0].IsPlayer);
        Assert.True(context.Nearby[1].IsPlayer);

        owner.Name = "changed-owner";
        earlierSerial.Name = "changed-nearby";

        Assert.Equal("owner", context.Self.Name);
        Assert.Equal("earlier", context.Nearby[0].Name);
    }

    [Fact]
    public async Task Create_DescriptorPerceptionRange_ExcludesMobileJustOutsideRange()
    {
        var persistence = new FakePersistenceService();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), new StubEventBus());
        var owner = await AddMobileAsync(persistence, spatial, 0x1, "owner", 0, 10, 10);
        var inRange = await AddMobileAsync(persistence, spatial, 0x2, "inside", 0, 14, 10);
        await AddMobileAsync(persistence, spatial, 0x3, "outside", 0, 15, 10);
        var factory = new NpcBrainContextFactory(
            spatial,
            new StubSessionManager(),
            new MutableTimeProvider(DateTimeOffset.UnixEpoch)
        );

        var context = factory.Create(owner, 0, owner.Position, new BrainDescriptor("guard", 1000, 4, 15));

        Assert.Equal([inRange.Id], context.Nearby.Select(snapshot => snapshot.Id));
    }

    private static async Task<MobileEntity> AddMobileAsync(
        FakePersistenceService persistence,
        SpatialIndexService spatial,
        uint serial,
        string name,
        int mapId,
        int x,
        int y
    )
    {
        var mobile = new MobileEntity
        {
            Id = new Serial(serial),
            Name = name,
            MapId = mapId,
            Position = new Point3D(x, y, 0),
            Hits = 25,
            HitsMax = 50,
            Warmode = true,
            CombatantId = new Serial(0x20),
            Criminal = true,
            Kills = 3
        };
        await persistence.Store<MobileEntity>().UpsertAsync(mobile);
        spatial.AddOrUpdate(mobile);

        return mobile;
    }
}
