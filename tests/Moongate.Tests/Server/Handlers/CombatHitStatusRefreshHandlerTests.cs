using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class CombatHitStatusRefreshHandlerTests
{
    private sealed class TestSpatialWorldService : RegionDataLoaderTestSpatialWorldService
    {
        public List<GameSession> SessionsInRange { get; } = [];

        public override List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;

            return SessionsInRange.ToList();
        }
    }

    [Test]
    public async Task HandleAsync_WhenNpcDefenderIsVisible_ShouldRefreshOnlyVisiblePlayerSessions()
    {
        var spatial = new TestSpatialWorldService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var handler = new CombatHitStatusRefreshHandler(spatial, outgoing);
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x300,
            Name = "guard",
            MapId = 1,
            Location = new(100, 100, 0),
            Hits = 34,
            MaxHits = 40,
            IsPlayer = false
        };

        using var nearClient = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        using var farClient = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var nearSession = new GameSession(new(nearClient))
        {
            CharacterId = (Serial)0x100,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x100,
                IsPlayer = true,
                MapId = 1,
                Location = new(101, 100, 0)
            },
            ViewRange = 18
        };
        var farSession = new GameSession(new(farClient))
        {
            CharacterId = (Serial)0x200,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x200,
                IsPlayer = true,
                MapId = 1,
                Location = new(150, 150, 0)
            },
            ViewRange = 18
        };
        spatial.SessionsInRange.Add(nearSession);
        spatial.SessionsInRange.Add(farSession);

        await handler.HandleAsync(new CombatHitEvent((Serial)0x111, defender.Id, defender.MapId, defender.Location, 6, new UOMobileEntity(), defender));

        Assert.Multiple(
            () =>
            {
                Assert.That(outgoing.TryDequeue(out var first), Is.True);
                Assert.That(first.SessionId, Is.EqualTo(nearSession.SessionId));
                Assert.That(first.Packet, Is.TypeOf<PlayerStatusPacket>());
                Assert.That(((PlayerStatusPacket)first.Packet).Mobile, Is.SameAs(defender));
                Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenDefenderIsPlayer_ShouldDoNothing()
    {
        var spatial = new TestSpatialWorldService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var handler = new CombatHitStatusRefreshHandler(spatial, outgoing);
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x300,
            Name = "player",
            MapId = 1,
            Location = new(100, 100, 0),
            Hits = 34,
            MaxHits = 40,
            IsPlayer = true
        };

        await handler.HandleAsync(new CombatHitEvent((Serial)0x111, defender.Id, defender.MapId, defender.Location, 6, new UOMobileEntity(), defender));

        Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
    }
}
