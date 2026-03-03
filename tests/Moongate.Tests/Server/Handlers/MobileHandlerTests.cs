using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Handlers;

public sealed class MobileHandlerTests
{
    private sealed class MobileHandlerTestCharacterService : ICharacterService
    {
        private readonly UOMobileEntity _mobile;

        public MobileHandlerTestCharacterService(UOMobileEntity mobile)
        {
            _mobile = mobile;
        }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(_mobile.Id == characterId ? _mobile : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class MobileHandlerTestSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> PlayersInSector { get; set; } = [];
        public List<GameSession> SessionsInRange { get; set; } = [];

        public MapSector? SectorByLocation { get; set; }
        public Func<int, Point3D, MapSector?>? SectorByLocationResolver { get; set; }
        public Dictionary<(int MapId, int SectorX, int SectorY), MapSector> SectorsByCoordinate { get; set; } = new();

        public int LastGetSectorMapId { get; private set; }

        public Point3D LastGetSectorLocation { get; private set; }

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => Task.FromResult(0);

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
        {
            _ = location;
            _ = range;
            _ = mapId;
            return excludeSession is null
                       ? [.. SessionsInRange]
                       : [.. SessionsInRange.Where(session => session != excludeSession)];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => PlayersInSector;

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
            => [];

        public List<MapSector> GetActiveSectors()
            => [];

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            LastGetSectorMapId = mapId;
            LastGetSectorLocation = location;

            if (SectorByLocationResolver is not null)
            {
                return SectorByLocationResolver(mapId, location);
            }

            var key = (mapId, location.X >> 4, location.Y >> 4);
            if (SectorsByCoordinate.TryGetValue(key, out var sector))
            {
                return sector;
            }

            return SectorByLocation;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    [Test]
    public async Task HandleAsync_ForMobileAddedInSector_ShouldSendPacketsToOtherPlayersOnly()
    {
        var mobileId = (Serial)0x00000010u;
        var receiverId = (Serial)0x00000020u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var receiverSession = CreateSession(receiverId);
        sessions.Add(receiverSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SessionsInRange = [receiverSession]
        };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(mobileId));
        var handler = new MobileHandler(
            spatial,
            characterService,
            new DispatchEventsService(spatial, queue),
            sessions,
            queue,
            new MoongateConfig()
        );

        await handler.HandleAsync(new MobileAddedInSectorEvent(mobileId, 1, 100, 200));

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets, Has.Count.EqualTo(2));
                Assert.That(packets.All(packet => packet.SessionId == receiverSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<MobileIncomingPacket>());
                Assert.That(packets[1].Packet, Is.TypeOf<PlayerStatusPacket>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_ShouldNotSendPackets_WhenSectorNotFound()
    {
        var mobileId = (Serial)0x00000300u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var spatial = new MobileHandlerTestSpatialWorldService { SectorByLocation = null };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(mobileId));
        var handler = new MobileHandler(
            spatial,
            characterService,
            new DispatchEventsService(spatial, queue),
            sessions,
            queue,
            new MoongateConfig()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                1,
                mobileId,
                1,
                new(0, 0, 0),
                new(1, 1, 0)
            )
        );

        Assert.That(queue.TryDequeue(out _), Is.False);
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_ShouldResolveTargetSectorAndSendPackets()
    {
        var mobileId = (Serial)0x00000100u;
        var receiverId = (Serial)0x00000200u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var receiverSession = CreateSession(receiverId);
        sessions.Add(receiverSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(1, 7, 8),
            SessionsInRange = [receiverSession]
        };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(mobileId));
        var handler = new MobileHandler(
            spatial,
            characterService,
            new DispatchEventsService(spatial, queue),
            sessions,
            queue,
            new MoongateConfig()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                99,
                mobileId,
                1,
                new(200, 200, 0),
                new(210, 210, 0)
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.LastGetSectorMapId, Is.EqualTo(1));
                Assert.That(spatial.LastGetSectorLocation, Is.EqualTo(new Point3D(210, 210, 0)));
                Assert.That(packets, Has.Count.EqualTo(1));
                Assert.That(packets.All(packet => packet.SessionId == receiverSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<MobileMovingPacket>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldSendSectorItemsAndMobilesToEnteringPlayer()
    {
        var movingPlayerId = (Serial)0x00001000u;
        var npcId = (Serial)0x00002000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(100, 100, 0);
        var newLocation = new Point3D(132, 132, 0);
        var oldSector = new MapSector(1, 6, 6);
        var newSector = new MapSector(1, 8, 8);

        newSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000010u,
                Name = "Ground Item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = newLocation,
                MapId = 1
            }
        );
        newSector.AddEntity(
            new UOMobileEntity
            {
                Id = npcId,
                IsPlayer = false,
                Name = "npc",
                Location = newLocation,
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            PlayersInSector = [],
            SectorByLocationResolver = (_, location) => location == oldLocation ? oldSector : newSector
        };
        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(movingPlayerId));
        var handler = new MobileHandler(
            spatial,
            characterService,
            new DispatchEventsService(spatial, queue),
            sessions,
            queue,
            new MoongateConfig()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                1,
                oldLocation,
                newLocation
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets.All(packet => packet.SessionId == movingSession.SessionId), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is ObjectInformationPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is MobileIncomingPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PlayerStatusPacket), Is.True);
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldSendSnapshotForNeighborSectorsWithinRadius()
    {
        var movingPlayerId = (Serial)0x00003000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(100, 100, 0);
        var newLocation = new Point3D(132, 132, 0); // sector (8,8)
        var centerSector = new MapSector(1, 8, 8);
        var neighborSector = new MapSector(1, 9, 8);

        centerSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000020u,
                Name = "center-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new(8 << 4, 8 << 4, 0),
                MapId = 1
            }
        );

        neighborSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000021u,
                Name = "neighbor-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new(9 << 4, 8 << 4, 0),
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, 8, 8)] = centerSector;
        spatial.SectorsByCoordinate[(1, 9, 8)] = neighborSector;
        spatial.SectorByLocationResolver = (_, location) =>
        {
            if (location == oldLocation)
            {
                return new MapSector(1, 6, 6);
            }

            if (location == newLocation)
            {
                return centerSector;
            }

            var key = (1, location.X >> 4, location.Y >> 4);
            return spatial.SectorsByCoordinate.TryGetValue(key, out var sector) ? sector : null;
        };

        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(movingPlayerId));
        var handler = new MobileHandler(
            spatial,
            characterService,
            new DispatchEventsService(spatial, queue),
            sessions,
            queue,
            new MoongateConfig
            {
                Spatial = new MoongateSpatialConfig
                {
                    SectorEnterSyncRadius = 1
                }
            }
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                1,
                oldLocation,
                newLocation
            )
        );

        var packets = DequeueAll(queue);
        var objectPackets = packets.Count(packet => packet.Packet is ObjectInformationPacket);

        Assert.That(objectPackets, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task HandleAsync_ForPlayerCharacterLoggedIn_ShouldSendSectorSnapshotToEnteringPlayer()
    {
        var movingPlayerId = (Serial)0x00004000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var spawnLocation = new Point3D(132, 132, 0);
        var centerSector = new MapSector(1, 8, 8);
        centerSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000031u,
                Name = "spawn-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = spawnLocation,
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, 8, 8)] = centerSector;
        spatial.SectorByLocationResolver = (_, location) =>
        {
            var key = (1, location.X >> 4, location.Y >> 4);
            return spatial.SectorsByCoordinate.TryGetValue(key, out var sector) ? sector : null;
        };

        var character = CreatePlayer(movingPlayerId);
        character.Location = spawnLocation;
        character.MapId = 1;
        var characterService = new MobileHandlerTestCharacterService(character);
        var handler = new MobileHandler(
            spatial,
            characterService,
            new DispatchEventsService(spatial, queue),
            sessions,
            queue,
            new MoongateConfig()
        );

        await handler.HandleAsync(
            new PlayerCharacterLoggedInEvent(
                movingSession.SessionId,
                (Serial)0x01u,
                movingPlayerId
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets.All(packet => packet.SessionId == movingSession.SessionId), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is ObjectInformationPacket), Is.True);
            }
        );
    }

    private static UOMobileEntity CreatePlayer(Serial id)
        => new()
        {
            Id = id,
            IsPlayer = true,
            Name = $"player-{id.Value}",
            Location = new(100, 100, 0),
            MapId = 1
        };

    private static GameSession CreateSession(Serial characterId)
    {
        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        return new(new(client))
        {
            CharacterId = characterId,
            Character = new()
            {
                Id = characterId
            }
        };
    }

    private static List<OutgoingGamePacket> DequeueAll(BasePacketListenerTestOutgoingPacketQueue queue)
    {
        var packets = new List<OutgoingGamePacket>();

        while (queue.TryDequeue(out var packet))
        {
            packets.Add(packet);
        }

        return packets;
    }
}
