using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class AnkhResurrectionOfferListenerTests
{
    [Test]
    public async Task HandleAsync_WhenDeadPlayerIsAddedNearAnkh_ShouldCreateOffer()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00003001u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(100, 100, 0)
        };
        var ankh = CreateAnkhItem((Serial)0x40003001u, new Point3D(101, 100, 0));
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var spatialWorldService = new TestSpatialWorldService();
        spatialWorldService.NearbyItems.Add(ankh);
        var resurrectionOfferService = new RecordingResurrectionOfferService();
        var listener = new AnkhResurrectionOfferListener(
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
                Assert.That(resurrectionOfferService.Calls[0].SourceType, Is.EqualTo(ResurrectionOfferSourceType.Ankh));
                Assert.That(resurrectionOfferService.Calls[0].SourceSerial, Is.EqualTo(ankh.Id));
                Assert.That(resurrectionOfferService.Calls[0].MapId, Is.EqualTo(ankh.MapId));
                Assert.That(resurrectionOfferService.Calls[0].SourceLocation, Is.EqualTo(ankh.Location));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenDeadPlayerStaysNearSameAnkh_ShouldNotCreateDuplicateOffer()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00003002u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 0,
            Location = new Point3D(120, 120, 0)
        };
        var ankh = CreateAnkhItem((Serial)0x40003002u, new Point3D(121, 120, 0));
        var session = new GameSession(new(client))
        {
            Character = player,
            CharacterId = player.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var spatialWorldService = new TestSpatialWorldService();
        spatialWorldService.NearbyItems.Add(ankh);
        var resurrectionOfferService = new RecordingResurrectionOfferService();
        var listener = new AnkhResurrectionOfferListener(
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
                new Point3D(119, 120, 0),
                player.Location
            )
        );

        Assert.That(resurrectionOfferService.Calls, Has.Count.EqualTo(1));
    }

    private static UOItemEntity CreateAnkhItem(Serial id, Point3D location)
    {
        var ankh = new UOItemEntity
        {
            Id = id,
            MapId = 0,
            Location = location
        };
        ankh.SetCustomString(ItemCustomParamKeys.Interaction.ResurrectionSource, "ankh");

        return ankh;
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

    private sealed class TestSpatialWorldService : ISpatialWorldService
    {
        public List<UOItemEntity> NearbyItems { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => _ = mobile;

        public void AddRegion(JsonRegion region)
            => _ = region;

        public Task<int> BroadcastToPlayersAsync(
            Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => Task.FromResult(0);

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [.. NearbyItems];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
            => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => [];

        public JsonRegion? GetRegionById(int regionId)
            => null;

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
            => null;

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
            => _ = serial;
    }
}
