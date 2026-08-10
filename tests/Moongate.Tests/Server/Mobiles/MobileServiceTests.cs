using Moongate.Core.Extensions;
using Moongate.Network.Packets.Outgoing;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Mobiles;

/// <summary>
/// Raw placement: no terrain validation, and the world is told only when something actually moved.
/// </summary>
public class MobileServiceTests
{
    [Fact]
    public void Teleport_MovesTheMobileAndAnnouncesItOnce()
    {
        var (service, persistence, moves) = Build(out var mobile);

        Assert.True(service.Teleport(mobile.Id, 10, 20, 5));

        Assert.Equal(new(10, 20, 5), persistence.Store<MobileEntity>().GetById(mobile.Id)!.Position);
        Assert.Single(moves);
    }

    // An NPC has no client, and a teleport that assumed one would throw on every spawner placement.
    [Fact]
    public void Teleport_AMobileWithNoSession_IsStillMoved()
    {
        var (service, persistence, _) = Build(out var mobile, out _);

        Assert.True(service.Teleport(mobile.Id, 10, 20, 5));
        Assert.Equal(new(10, 20, 5), persistence.Store<MobileEntity>().GetById(mobile.Id)!.Position);
    }

    // The guard MobileModule already had, and the reason this is not a blind write.
    [Fact]
    public void Teleport_ToWhereItAlreadyIs_AnnouncesNothing()
    {
        var (service, _, moves) = Build(out var mobile);

        Assert.True(service.Teleport(mobile.Id, mobile.Position.X, mobile.Position.Y, mobile.Position.Z));

        Assert.Empty(moves);
    }

    [Fact]
    public void Teleport_UnknownSerial_IsRefused()
    {
        var (service, _, moves) = Build(out _);

        Assert.False(service.Teleport((Serial)0xDEAD, 1, 2, 3));
        Assert.Empty(moves);
    }

    private static (MobileService Service, FakePersistenceService Persistence, List<MobileMovedEvent> Moves) Build(
        out MobileEntity mobile
    )
        => Build(out mobile, out _);

    private static (MobileService Service, FakePersistenceService Persistence, List<MobileMovedEvent> Moves) Build(
        out MobileEntity mobile,
        out StubSessionManager sessions
    )
    {
        var persistence = new FakePersistenceService();
        var events = new EventBusService();
        var moves = new List<MobileMovedEvent>();

        events.Subscribe<MobileMovedEvent>(
            (message, _) =>
            {
                moves.Add(message);

                return Task.CompletedTask;
            }
        );

        mobile = new() { Name = "Squid", MapId = 1, Position = new(1, 1, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), events);

        sessions = new();

        return (new(persistence, spatial, events, sessions), persistence, moves);
    }
}
