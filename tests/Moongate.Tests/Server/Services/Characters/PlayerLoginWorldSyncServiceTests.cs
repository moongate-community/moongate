using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Characters;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Characters;

public sealed class PlayerLoginWorldSyncServiceTests
{
    private sealed class PlayerLoginWorldSyncTestSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> PlayersInSector { get; } = [];
        public List<GameSession> SessionsInRange { get; } = [];
        public List<UOItemEntity> NearbyItems { get; set; } = [];
        public List<UOMobileEntity> NearbyMobiles { get; } = [];
        public List<MapSector> ActiveSectors { get; } = [];

        public MapSector? SectorByLocation { get; set; }
        public Func<int, Point3D, MapSector?>? SectorByLocationResolver { get; set; }
        public Dictionary<(int MapId, int SectorX, int SectorY), MapSector> SectorsByCoordinate { get; } = new();

        public int LastGetSectorMapId { get; private set; }

        public Point3D LastGetSectorLocation { get; private set; }

        public int GetSectorByLocationCallCount { get; private set; }

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
            => Task.FromResult(0);

        public List<MapSector> GetActiveSectors()
            => ActiveSectors.Count > 0 ? [.. ActiveSectors] : [.. SectorsByCoordinate.Values];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return NearbyItems;
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return NearbyMobiles;
        }

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

        public JsonRegion? GetRegionById(int regionId)
            => null;

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            LastGetSectorMapId = mapId;
            LastGetSectorLocation = location;
            GetSectorByLocationCallCount++;

            if (SectorByLocationResolver is not null)
            {
                return SectorByLocationResolver(mapId, location);
            }

            return SectorByLocation;
        }

        public SectorSystemStats GetStats()
            => new();

        public IReadOnlyCollection<int> GetSubscribedRegionIds()
            => Array.Empty<int>();

        public int GetUpdateBroadcastSectorRadius()
            => 1;

        public bool IsNearRegionEdge(int mapId, Point3D location, int threshold)
            => false;

        public void MoveItem(UOItemEntity item, int oldMapId, Point3D oldLocation) { }

        public void MoveMobile(UOMobileEntity mobile, int oldMapId, Point3D oldLocation) { }

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }

        public bool RemoveItem(Serial itemId)
            => true;

        public bool RemoveMobile(Serial mobileId)
            => true;

        public Task WarmupAroundSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            int radius,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;
    }

    [Test]
    public async Task SyncAsync_ShouldFilterItemsBySessionAccountType()
    {
        var playerId = (Serial)0x00004010u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var session = CreateSession(playerId);
        session.AccountType = AccountType.Regular;
        var spawnLocation = new Point3D(143, 132, 0);
        var sectorX = spawnLocation.X >> MapSectorConsts.SectorShift;
        var sectorY = spawnLocation.Y >> MapSectorConsts.SectorShift;
        var sector = new MapSector(1, sectorX, sectorY);
        sector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000071u,
                Name = "gm-only-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Visibility = AccountType.GameMaster,
                Location = spawnLocation,
                MapId = 1
            }
        );

        var spatial = new PlayerLoginWorldSyncTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, sectorX, sectorY)] = sector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var resolved)
                                                          ? resolved
                                                          : null;
                                           };

        var character = CreatePlayer(playerId, spawnLocation);
        session.Character = character;
        var service = new PlayerLoginWorldSyncService(spatial, queue, new());

        await service.SyncAsync(session, character);

        var packets = DequeueAll(queue);

        Assert.That(packets.Any(packet => packet.Packet is ObjectInformationPacket), Is.False);
    }

    [Test]
    public async Task SyncAsync_ShouldRefillVisibleRangeOutsideLoginSnapshotRadius()
    {
        var playerId = (Serial)0x00004020u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var session = CreateSession(playerId);

        var spawnLocation = new Point3D(143, 132, 0);
        var centerSectorX = spawnLocation.X >> MapSectorConsts.SectorShift;
        var centerSectorY = spawnLocation.Y >> MapSectorConsts.SectorShift;
        var centerSector = new MapSector(1, centerSectorX, centerSectorY);
        centerSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000091u,
                Name = "center-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = spawnLocation,
                MapId = 1
            }
        );

        var outerLocation = new Point3D((centerSectorX + 2) << MapSectorConsts.SectorShift, spawnLocation.Y, 0);
        var outerSectorX = outerLocation.X >> MapSectorConsts.SectorShift;
        var outerSectorY = outerLocation.Y >> MapSectorConsts.SectorShift;
        var outerSector = new MapSector(1, outerSectorX, outerSectorY);
        var outerItemId = (Serial)0x40000092u;
        outerSector.AddEntity(
            new UOItemEntity
            {
                Id = outerItemId,
                Name = "outer-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = outerLocation,
                MapId = 1
            }
        );

        var spatial = new PlayerLoginWorldSyncTestSpatialWorldService
        {
            NearbyItems =
            [
                new()
                {
                    Id = outerItemId,
                    Name = "outer-item",
                    ItemId = 0x0EED,
                    ParentContainerId = Serial.Zero,
                    EquippedMobileId = Serial.Zero,
                    Location = outerLocation,
                    MapId = 1
                }
            ]
        };
        spatial.SectorsByCoordinate[(1, centerSectorX, centerSectorY)] = centerSector;
        spatial.SectorsByCoordinate[(1, outerSectorX, outerSectorY)] = outerSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var resolved)
                                                          ? resolved
                                                          : null;
                                           };

        var character = CreatePlayer(playerId, spawnLocation);
        session.Character = character;
        var service = new PlayerLoginWorldSyncService(spatial, queue, new());

        await service.SyncAsync(session, character);

        var packets = DequeueAll(queue);
        var objectPackets = packets
                            .Where(packet => packet.Packet is ObjectInformationPacket)
                            .Select(packet => (ObjectInformationPacket)packet.Packet)
                            .ToList();

        Assert.That(objectPackets.Select(packet => packet.Serial), Contains.Item(outerItemId));
    }

    [Test]
    public async Task SyncAsync_ShouldNotRefillItemsOutsideSessionViewRange()
    {
        var playerId = (Serial)0x00004021u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var session = CreateSession(playerId);
        session.ViewRange = 5;

        var spawnLocation = new Point3D(132, 132, 0);
        var centerSectorX = spawnLocation.X >> MapSectorConsts.SectorShift;
        var centerSectorY = spawnLocation.Y >> MapSectorConsts.SectorShift;
        var centerSector = new MapSector(1, centerSectorX, centerSectorY);

        var outerLocation = new Point3D(spawnLocation.X + MapSectorConsts.SectorSize * 2, spawnLocation.Y, 0);
        var outerSectorX = outerLocation.X >> MapSectorConsts.SectorShift;
        var outerSectorY = outerLocation.Y >> MapSectorConsts.SectorShift;
        var outerItemId = (Serial)0x40000093u;
        var outerSector = new MapSector(1, outerSectorX, outerSectorY);
        outerSector.AddEntity(
            new UOItemEntity
            {
                Id = outerItemId,
                Name = "outer-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = outerLocation,
                MapId = 1
            }
        );

        var spatial = new PlayerLoginWorldSyncTestSpatialWorldService
        {
            NearbyItems =
            [
                new()
                {
                    Id = outerItemId,
                    Name = "outer-item",
                    ItemId = 0x0EED,
                    ParentContainerId = Serial.Zero,
                    EquippedMobileId = Serial.Zero,
                    Location = outerLocation,
                    MapId = 1
                }
            ]
        };
        spatial.SectorsByCoordinate[(1, centerSectorX, centerSectorY)] = centerSector;
        spatial.SectorsByCoordinate[(1, outerSectorX, outerSectorY)] = outerSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var resolved)
                                                          ? resolved
                                                          : null;
                                           };

        var character = CreatePlayer(playerId, spawnLocation);
        session.Character = character;
        var service = new PlayerLoginWorldSyncService(spatial, queue, new());

        await service.SyncAsync(session, character);

        var packets = DequeueAll(queue);
        var objectPackets = packets
                            .Where(packet => packet.Packet is ObjectInformationPacket)
                            .Select(packet => (ObjectInformationPacket)packet.Packet)
                            .ToList();

        Assert.That(objectPackets.Select(packet => packet.Serial), Does.Not.Contain(outerItemId));
    }

    [Test]
    public async Task SyncAsync_ShouldSendSectorSnapshotToEnteringPlayer()
    {
        var playerId = (Serial)0x00004000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var session = CreateSession(playerId);
        var spawnLocation = new Point3D(132, 132, 0);
        var centerSectorX = spawnLocation.X >> MapSectorConsts.SectorShift;
        var centerSectorY = spawnLocation.Y >> MapSectorConsts.SectorShift;
        var centerSector = new MapSector(1, centerSectorX, centerSectorY);
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

        var spatial = new PlayerLoginWorldSyncTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, centerSectorX, centerSectorY)] = centerSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var character = CreatePlayer(playerId, spawnLocation);
        session.Character = character;
        var service = new PlayerLoginWorldSyncService(spatial, queue, new());

        await service.SyncAsync(session, character);

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets.All(packet => packet.SessionId == session.SessionId), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is ObjectInformationPacket), Is.True);
            }
        );
    }

    [Test]
    public async Task SyncAsync_WhenCorpseIsVisible_ShouldSendCorpseContentsAndClothing()
    {
        var playerId = (Serial)0x00004031u;
        var corpseId = (Serial)0x400000A1u;
        var chestId = (Serial)0x400000A2u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var session = CreateSession(playerId);
        var spawnLocation = new Point3D(132, 132, 0);
        var centerSectorX = spawnLocation.X >> MapSectorConsts.SectorShift;
        var centerSectorY = spawnLocation.Y >> MapSectorConsts.SectorShift;
        var centerSector = new MapSector(1, centerSectorX, centerSectorY);
        var corpse = new UOItemEntity
        {
            Id = corpseId,
            Name = "a corpse",
            ItemId = 0x2006,
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero,
            Location = spawnLocation,
            MapId = 1
        };
        corpse.SetCustomBoolean("is_corpse", true);
        var chest = new UOItemEntity
        {
            Id = chestId,
            ItemId = 0x1415,
            ParentContainerId = corpseId
        };
        chest.SetCustomInteger("corpse_equipped_layer", (byte)ItemLayerType.InnerTorso);
        corpse.AddItem(chest, Point2D.Zero);
        centerSector.AddEntity(corpse);

        var spatial = new PlayerLoginWorldSyncTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, centerSectorX, centerSectorY)] = centerSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var character = CreatePlayer(playerId, spawnLocation);
        session.Character = character;
        var service = new PlayerLoginWorldSyncService(spatial, queue, new());

        await service.SyncAsync(session, character);

        var packets = DequeueAll(queue).Select(outbound => outbound.Packet.GetType()).ToList();

        Assert.Multiple(
            () =>
            {
                Assert.That(packets, Does.Contain(typeof(ObjectInformationPacket)));
                Assert.That(packets, Does.Contain(typeof(AddMultipleItemsToContainerPacket)));
                Assert.That(packets, Does.Contain(typeof(CorpseClothingPacket)));
            }
        );
    }

    [Test]
    public async Task SyncAsync_ShouldNotSendMountedCreaturesAsStandaloneMobiles()
    {
        var playerId = (Serial)0x00004030u;
        var mountedCreatureId = (Serial)0x00005030u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var session = CreateSession(playerId);
        var spawnLocation = new Point3D(132, 132, 0);
        var sectorX = spawnLocation.X >> MapSectorConsts.SectorShift;
        var sectorY = spawnLocation.Y >> MapSectorConsts.SectorShift;
        var sector = new MapSector(1, sectorX, sectorY);
        sector.AddEntity(
            new UOMobileEntity
            {
                Id = mountedCreatureId,
                Name = "mounted-horse",
                Location = spawnLocation,
                MapId = 1,
                RiderMobileId = (Serial)0x00005031u
            }
        );

        var spatial = new PlayerLoginWorldSyncTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, sectorX, sectorY)] = sector;
        spatial.NearbyMobiles.Add(
            new UOMobileEntity
            {
                Id = mountedCreatureId,
                Name = "mounted-horse",
                Location = spawnLocation,
                MapId = 1,
                RiderMobileId = (Serial)0x00005031u
            }
        );
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var resolved)
                                                          ? resolved
                                                          : null;
                                           };

        var character = CreatePlayer(playerId, spawnLocation);
        session.Character = character;
        var service = new PlayerLoginWorldSyncService(spatial, queue, new());

        await service.SyncAsync(session, character);

        var packets = DequeueAll(queue);

        Assert.That(packets.Any(packet => packet.Packet is MobileIncomingPacket), Is.False);
    }

    private static UOMobileEntity CreatePlayer(Serial id, Point3D location)
        => new()
        {
            Id = id,
            IsPlayer = true,
            Name = $"player-{id.Value}",
            Location = location,
            MapId = 1
        };

    private static GameSession CreateSession(Serial characterId)
    {
        var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        return new(new(client))
        {
            AccountId = (Serial)0x01u,
            AccountType = AccountType.Regular,
            CharacterId = characterId
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
