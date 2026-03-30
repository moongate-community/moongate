using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Types.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class HealerResurrectionOfferListenerTests
{
    [Test]
    public async Task HandleAsync_WhenDeadPlayerIsAddedNearHealer_ShouldCreateOffer()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00001001u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(100, 100, 0)
        };
        var healer = CreateHealerMobile((Serial)0x00002001u, new Point3D(102, 101, 0));
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var spatialWorldService = new TestSpatialWorldService();
        spatialWorldService.NearbyMobiles.Add(healer);
        var resurrectionOfferService = new RecordingResurrectionOfferService();
        var listener = new HealerResurrectionOfferListener(
            spatialWorldService,
            sessionService,
            resurrectionOfferService
        );

        await listener.HandleAsync(new MobileAddedInWorldEvent(player));

        Assert.Multiple(
            () =>
            {
                Assert.That(resurrectionOfferService.Calls, Has.Count.EqualTo(1));
                Assert.That(resurrectionOfferService.Calls[0].SessionId, Is.EqualTo(session.SessionId));
                Assert.That(resurrectionOfferService.Calls[0].CharacterId, Is.EqualTo(player.Id));
                Assert.That(resurrectionOfferService.Calls[0].SourceType, Is.EqualTo(ResurrectionOfferSourceType.Healer));
                Assert.That(resurrectionOfferService.Calls[0].SourceSerial, Is.EqualTo(healer.Id));
                Assert.That(resurrectionOfferService.Calls[0].MapId, Is.EqualTo(healer.MapId));
                Assert.That(resurrectionOfferService.Calls[0].SourceLocation, Is.EqualTo(healer.Location));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenDeadPlayerEntersHealerRange_ShouldCreateOffer()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00001002u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(100, 100, 0)
        };
        var healer = CreateHealerMobile((Serial)0x00002002u, new Point3D(101, 100, 0));
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var spatialWorldService = new TestSpatialWorldService
        {
            NearbyMobilesFactory = (location, _, _) =>
            {
                if (location == new Point3D(90, 90, 0))
                {
                    return [];
                }

                return [healer];
            }
        };
        var resurrectionOfferService = new RecordingResurrectionOfferService();
        var listener = new HealerResurrectionOfferListener(
            spatialWorldService,
            sessionService,
            resurrectionOfferService
        );

        await listener.HandleAsync(
            new MobilePositionChangedEvent(
                session.SessionId,
                player.Id,
                0,
                0,
                new Point3D(90, 90, 0),
                player.Location
            )
        );

        Assert.That(resurrectionOfferService.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_WhenDeadPlayerStaysWithinHealerRange_ShouldNotCreateDuplicateOffer()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00001003u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(100, 100, 0)
        };
        var healer = CreateHealerMobile((Serial)0x00002003u, new Point3D(101, 100, 0));
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var spatialWorldService = new TestSpatialWorldService();
        spatialWorldService.NearbyMobiles.Add(healer);
        var resurrectionOfferService = new RecordingResurrectionOfferService();
        var listener = new HealerResurrectionOfferListener(
            spatialWorldService,
            sessionService,
            resurrectionOfferService
        );

        await listener.HandleAsync(new MobileAddedInWorldEvent(player));
        await listener.HandleAsync(
            new MobilePositionChangedEvent(
                session.SessionId,
                player.Id,
                0,
                0,
                new Point3D(99, 100, 0),
                player.Location
            )
        );

        Assert.That(resurrectionOfferService.Calls, Has.Count.EqualTo(1));
    }

    private static UOMobileEntity CreateHealerMobile(Serial id, Point3D location)
    {
        var healer = new UOMobileEntity
        {
            Id = id,
            IsPlayer = false,
            IsAlive = true,
            MapId = 0,
            Location = location
        };
        healer.SetCustomString(MobileCustomParamKeys.Interaction.ResurrectionSource, "healer");

        return healer;
    }

    private sealed class RecordingResurrectionOfferService : IResurrectionOfferService
    {
        public sealed record OfferCall(
            long SessionId,
            Serial CharacterId,
            ResurrectionOfferSourceType SourceType,
            Serial SourceSerial,
            int MapId,
            Point3D SourceLocation
        );

        public List<OfferCall> Calls { get; } = [];

        public void Decline(long sessionId)
            => _ = sessionId;

        public Task<bool> TryAcceptAsync(long sessionId, CancellationToken cancellationToken = default)
        {
            _ = sessionId;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<bool> TryCreateOfferAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            Calls.Add(new(sessionId, characterId, sourceType, characterId, 0, default));

            return Task.FromResult(true);
        }

        public Task<bool> TryCreateOfferAsync(
            long sessionId,
            Serial characterId,
            ResurrectionOfferSourceType sourceType,
            Serial sourceSerial,
            int mapId,
            Point3D sourceLocation,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            Calls.Add(new(sessionId, characterId, sourceType, sourceSerial, mapId, sourceLocation));

            return Task.FromResult(true);
        }
    }

    private sealed class TestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly List<GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions.Add(session);

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
        {
            var removed = _sessions.RemoveAll(session => session.SessionId == sessionId);

            return removed > 0;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.SessionId == sessionId)!;

            return session is not null;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class TestSpatialWorldService : RegionDataLoaderTestSpatialWorldService, Moongate.Server.Interfaces.Services.Spatial.ISpatialWorldService
    {
        public Func<Point3D, int, int, List<UOMobileEntity>>? NearbyMobilesFactory { get; init; }

        public List<UOMobileEntity> NearbyMobiles { get; } = [];

        List<UOMobileEntity> Moongate.Server.Interfaces.Services.Spatial.ISpatialWorldService.GetNearbyMobiles(Point3D location, int range, int mapId)
            => NearbyMobilesFactory?.Invoke(location, range, mapId) ?? [.. NearbyMobiles];
    }
}
