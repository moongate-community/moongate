using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Events;

public sealed class DispatchEventsServiceTests
{
    [Test]
    public async Task HandleAsync_ForMobilePlaySoundEvent_ShouldBroadcastPlaySoundPacket()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new MobilePlaySoundEvent(
            mobileId: (Serial)0x00000002u,
            mapId: 1,
            location: new Point3D(111, 222, 7),
            soundModel: 0x0210,
            mode: 0x01,
            unknown3: 0
        );

        await service.HandleAsync(gameEvent);

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastCallCount, Is.EqualTo(1));
                Assert.That(spatial.LastMapId, Is.EqualTo(1));
                Assert.That(spatial.LastLocation, Is.EqualTo(new Point3D(111, 222, 7)));
                Assert.That(spatial.LastPacket, Is.TypeOf<PlaySoundEffectPacket>());
            }
        );

        var packet = (PlaySoundEffectPacket)spatial.LastPacket!;
        Assert.Multiple(
            () =>
            {
                Assert.That(packet.Mode, Is.EqualTo(0x01));
                Assert.That(packet.SoundModel, Is.EqualTo(0x0210));
                Assert.That(packet.Unknown3, Is.EqualTo(0));
                Assert.That(packet.Location, Is.EqualTo(new Point3D(111, 222, 7)));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForPlaySoundToPlayerEvent_ShouldEnqueuePacketOnlyForTargetCharacterSession()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var targetCharacterId = (Serial)0x00000022u;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        sessions.Map[targetCharacterId] = new GameSession(new(client))
        {
            CharacterId = targetCharacterId
        };
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new PlaySoundToPlayerEvent(
            targetCharacterId,
            new Point3D(50, 60, 7),
            0x0201,
            0x01,
            0
        );

        await service.HandleAsync(gameEvent);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(sessions.Map[targetCharacterId].SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<PlaySoundEffectPacket>());
                Assert.That(spatial.BroadcastCallCount, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePlayEffectEvent_ShouldBroadcastParticleEffectPacket()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new MobilePlayEffectEvent(
            mobileId: (Serial)0x00000002u,
            mapId: 1,
            location: new Point3D(111, 222, 7),
            itemId: 0x3728,
            speed: 10,
            duration: 10,
            effect: 2023
        );

        await service.HandleAsync(gameEvent);

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastCallCount, Is.EqualTo(1));
                Assert.That(spatial.LastMapId, Is.EqualTo(1));
                Assert.That(spatial.LastLocation, Is.EqualTo(new Point3D(111, 222, 7)));
                Assert.That(spatial.LastPacket, Is.TypeOf<ParticleEffectPacket>());
            }
        );

        var packet = (ParticleEffectPacket)spatial.LastPacket!;
        Assert.Multiple(
            () =>
            {
                Assert.That(packet.ItemId, Is.EqualTo(0x3728));
                Assert.That(packet.SourceLocation, Is.EqualTo(new Point3D(111, 222, 7)));
                Assert.That(packet.TargetLocation, Is.EqualTo(new Point3D(111, 222, 7)));
                Assert.That(packet.Speed, Is.EqualTo(10));
                Assert.That(packet.Duration, Is.EqualTo(10));
                Assert.That(packet.Effect, Is.EqualTo(2023));
                Assert.That(packet.Layer, Is.EqualTo(0xFF));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForPlayEffectToPlayerEvent_ShouldEnqueuePacketOnlyForTargetCharacterSession()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var targetCharacterId = (Serial)0x00000022u;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        sessions.Map[targetCharacterId] = new GameSession(new(client))
        {
            CharacterId = targetCharacterId
        };
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new PlayEffectToPlayerEvent(
            targetCharacterId,
            new Point3D(10, 20, 3),
            itemId: 0x3728,
            speed: 8,
            duration: 12,
            effect: 5023
        );

        await service.HandleAsync(gameEvent);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(sessions.Map[targetCharacterId].SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<ParticleEffectPacket>());
                Assert.That(spatial.BroadcastCallCount, Is.EqualTo(0));
            }
        );

        var packet = (ParticleEffectPacket)outbound.Packet;
        Assert.Multiple(
            () =>
            {
                Assert.That(packet.ItemId, Is.EqualTo(0x3728));
                Assert.That(packet.Effect, Is.EqualTo(5023));
                Assert.That(packet.Speed, Is.EqualTo(8));
                Assert.That(packet.Duration, Is.EqualTo(12));
            }
        );
    }

    private sealed class DispatchEventsTestSpatialWorldService : ISpatialWorldService
    {
        public int BroadcastCallCount { get; private set; }

        public IGameNetworkPacket? LastPacket { get; private set; }

        public int LastMapId { get; private set; }

        public Point3D LastLocation { get; private set; } = Point3D.Zero;

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = range;
            _ = excludeSessionId;
            BroadcastCallCount++;
            LastPacket = packet;
            LastMapId = mapId;
            LastLocation = location;

            return Task.FromResult(1);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }

        public void AddRegion(JsonRegion region) { }

        public JsonRegion? GetRegionById(int regionId)
            => null;

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
            => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public List<MapSector> GetActiveSectors()
            => [];

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
            => null;

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    private sealed class DispatchEventsTestGameNetworkSessionService : IGameNetworkSessionService
    {
        public Dictionary<Serial, GameSession> Map { get; } = [];

        public int Count => Map.Count;

        public void Clear()
            => Map.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => [.. Map.Values];

        public GameSession GetOrCreate(Moongate.Network.Client.MoongateTCPClient client)
        {
            _ = client;
            throw new NotSupportedException();
        }

        public bool Remove(long sessionId)
        {
            var key = Map.FirstOrDefault(pair => pair.Value.SessionId == sessionId).Key;

            if (key == Serial.Zero)
            {
                return false;
            }

            return Map.Remove(key);
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = Map.Values.FirstOrDefault(value => value.SessionId == sessionId)!;

            return session is not null;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
            => Map.TryGetValue(characterId, out session!);
    }
}
