using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Events;

public sealed class DispatchEventsServiceTests
{
    private sealed class DispatchEventsTestSpatialWorldService : ISpatialWorldService
    {
        public int BroadcastCallCount { get; private set; }

        public IGameNetworkPacket? LastPacket { get; private set; }

        public int LastMapId { get; private set; }

        public Point3D LastLocation { get; private set; } = Point3D.Zero;

        public List<GameSession> PlayersInRange { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }

        public void AddRegion(JsonRegion region) { }

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

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

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
            => [.. PlayersInRange.Where(
                session =>
                    session != excludeSession &&
                    session.Character is not null &&
                    session.Character.MapId == mapId &&
                    session.Character.Location.InRange(location, range)
            )];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => [];

        public JsonRegion? GetRegionById(int regionId)
            => null;

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

        public GameSession GetOrCreate(MoongateTCPClient client)
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

    private sealed class DispatchEventsTestNotorietyService : INotorietyService
    {
        public int ComputeCallCount { get; private set; }

        public Notoriety Compute(UOMobileEntity source, UOMobileEntity target)
        {
            ComputeCallCount++;

            return target.Notoriety == Notoriety.Enemy
                       ? Notoriety.CanBeAttacked
                       : target.Notoriety;
        }
    }

    [Test]
    public async Task HandleAsync_ForMobilePlayAnimationEvent_ShouldBroadcastMobileAnimationPacket()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var service = new DispatchEventsService(spatial, queue, sessions);

        await service.HandleAsync(
            new MobilePlayAnimationEvent(
                (Serial)0x00000002u,
                1,
                new(111, 222, 7),
                17,
                7,
                1,
                true,
                false,
                0
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastCallCount, Is.EqualTo(1));
                Assert.That(spatial.LastMapId, Is.EqualTo(1));
                Assert.That(spatial.LastLocation, Is.EqualTo(new Point3D(111, 222, 7)));
                Assert.That(spatial.LastPacket, Is.TypeOf<MobileAnimationPacket>());
            }
        );

        var packet = (MobileAnimationPacket)spatial.LastPacket!;
        Assert.Multiple(
            () =>
            {
                Assert.That(packet.MobileId, Is.EqualTo((Serial)0x00000002u));
                Assert.That(packet.Action, Is.EqualTo(17));
                Assert.That(packet.FrameCount, Is.EqualTo(7));
                Assert.That(packet.RepeatCount, Is.EqualTo(1));
                Assert.That(packet.Forward, Is.True);
                Assert.That(packet.Repeat, Is.False);
                Assert.That(packet.Delay, Is.EqualTo(0));
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
            (Serial)0x00000002u,
            1,
            new(111, 222, 7),
            0x3728,
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
    public async Task HandleAsync_ForMobilePlaySoundEvent_ShouldBroadcastPlaySoundPacket()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new MobilePlaySoundEvent(
            (Serial)0x00000002u,
            1,
            new(111, 222, 7),
            0x0210
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
    public async Task HandleAsync_ForMobileWarModeChangedEvent_ShouldBroadcastMobileMovingPacket()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var notoriety = new DispatchEventsTestNotorietyService();
        var service = new DispatchEventsService(spatial, queue, sessions, notoriety);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            MapId = 1,
            Location = new(111, 222, 7),
            IsWarMode = true,
            Notoriety = Notoriety.Enemy
        };
        var viewerCharacter = new UOMobileEntity
        {
            Id = (Serial)0x00000003u,
            MapId = 1,
            Location = new(112, 222, 7),
            Notoriety = Notoriety.Innocent,
            IsPlayer = true
        };
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var viewerSession = new GameSession(new(client))
        {
            CharacterId = viewerCharacter.Id,
            Character = viewerCharacter
        };
        sessions.Map[viewerCharacter.Id] = viewerSession;
        spatial.PlayersInRange.Add(viewerSession);

        await service.HandleAsync(new MobileWarModeChangedEvent(mobile));
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastCallCount, Is.EqualTo(0));
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(viewerSession.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<MobileMovingPacket>());
                Assert.That(queue.TryDequeue(out _), Is.False);
                Assert.That(notoriety.ComputeCallCount, Is.EqualTo(1));
            }
        );

        var packet = (MobileMovingPacket)outbound.Packet;
        Assert.That(packet.Mobile, Is.Not.Null);
        Assert.That(packet.Mobile!.IsWarMode, Is.True);
        Assert.That(packet.ResolvedNotoriety, Is.EqualTo(Notoriety.CanBeAttacked));
    }

    [Test]
    public async Task DispatchMobileUpdateAsync_ShouldSkipMountedCreatureStandaloneUpdates()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var service = new DispatchEventsService(spatial, queue, sessions);
        var mountedCreature = new UOMobileEntity
        {
            Id = (Serial)0x00000090u,
            MapId = 1,
            Location = new(100, 100, 0),
            RiderMobileId = (Serial)0x00000091u
        };

        var recipients = await service.DispatchMobileUpdateAsync(mountedCreature, 1, 18, true);

        Assert.Multiple(
            () =>
            {
                Assert.That(recipients, Is.EqualTo(0));
                Assert.That(queue.TryDequeue(out _), Is.False);
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
        sessions.Map[targetCharacterId] = new(new(client))
        {
            CharacterId = targetCharacterId
        };
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new PlayEffectToPlayerEvent(
            targetCharacterId,
            new(10, 20, 3),
            0x3728,
            8,
            12,
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

    [Test]
    public async Task HandleAsync_ForPlaySoundToPlayerEvent_ShouldEnqueuePacketOnlyForTargetCharacterSession()
    {
        var spatial = new DispatchEventsTestSpatialWorldService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new DispatchEventsTestGameNetworkSessionService();
        var targetCharacterId = (Serial)0x00000022u;
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        sessions.Map[targetCharacterId] = new(new(client))
        {
            CharacterId = targetCharacterId
        };
        var service = new DispatchEventsService(spatial, queue, sessions);
        var gameEvent = new PlaySoundToPlayerEvent(
            targetCharacterId,
            new(50, 60, 7),
            0x0201
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
}
