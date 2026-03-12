using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Services.Events;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Handlers;

public sealed class MobileHandlerTests
{
    private sealed class MobileHandlerTestSpeechService : ISpeechService
    {
        public List<string> DirectMessages { get; } = [];

        public Task<int> BroadcastFromServerAsync(string text, short hue = 946, short font = 3, string language = "ENU")
            => Task.FromResult(0);

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<UnicodeSpeechMessagePacket?>(null);

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            _ = session;
            _ = hue;
            _ = font;
            _ = language;
            DirectMessages.Add(text);

            return Task.FromResult(true);
        }

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = 0x3B2,
            short font = 3,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(0);
    }

    private sealed class MobileHandlerTestCharacterService : ICharacterService
    {
        private readonly UOMobileEntity _mobile;

        public MobileHandlerTestCharacterService(UOMobileEntity mobile)
        {
            _mobile = mobile;
        }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(_mobile.Id == characterId ? _mobile : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class MobileHandlerTestNullCharacterService : ICharacterService
    {
        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(null);

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
        public Dictionary<(int MapId, int SectorX, int SectorY), MapSector> SectorsByCoordinate { get; } = new();

        public int LastGetSectorMapId { get; private set; }

        public Point3D LastGetSectorLocation { get; private set; }

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
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
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

            if (SectorByLocationResolver is not null)
            {
                return SectorByLocationResolver(mapId, location);
            }

            var key = (mapId, location.X >> MapSectorConsts.SectorShift, location.Y >> MapSectorConsts.SectorShift);

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
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
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
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                1,
                mobileId,
                1,
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
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                99,
                mobileId,
                1,
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
    public async Task HandleAsync_ForMobilePositionChanged_WhenEnteringAdjacentSector_ShouldOnlySyncDeltaSectors()
    {
        var movingPlayerId = (Serial)0x00006000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        // Move from sector (7,8) to adjacent sector (8,8) with radius 1
        // Old grid: (6,7)-(8,9) = 9 sectors
        // New grid: (7,7)-(9,9) = 9 sectors
        // Near-player sectors (within 1 of new center) are always re-synced
        // because the UO client drops items beyond its visual range.
        // Overlap sector (7,8) is near player → re-sent.
        // Delta sector (9,8) is new → sent.
        var oldLocation = new Point3D(7 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0);
        var newLocation = new Point3D(8 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0);

        var overlapSector = new MapSector(1, 7, 8);
        overlapSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000060u,
                Name = "overlap-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new(7 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0),
                MapId = 1
            }
        );

        var deltaSector = new MapSector(1, 9, 8);
        deltaSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000061u,
                Name = "delta-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new(9 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0),
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, 7, 8)] = overlapSector;
        spatial.SectorsByCoordinate[(1, 8, 8)] = new(1, 8, 8);
        spatial.SectorsByCoordinate[(1, 9, 8)] = deltaSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(movingPlayerId));
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
            {
                Spatial = new()
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
                1,
                oldLocation,
                newLocation
            )
        );

        var packets = DequeueAll(queue);
        var objectPackets = packets.Where(p => p.Packet is ObjectInformationPacket).ToList();

        Assert.Multiple(
            () =>
            {
                // Both overlap-item (near player, always re-synced) and delta-item are sent
                Assert.That(objectPackets, Has.Count.EqualTo(2));
                Assert.That(
                    packets.All(packet => packet.SessionId == movingSession.SessionId),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldSendSectorItemsAndMobilesToEnteringPlayer()
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
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                1,
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
                Assert.That(speechService.DirectMessages, Has.Count.EqualTo(1));
                Assert.That(speechService.DirectMessages[0], Does.Contain("Items: 1 e Mobiles: 1"));
            }
        );
    }

    [Test]
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldSendSnapshotForNeighborSectorsWithinRadius()
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
                Location = new(8 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0),
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
                Location = new(9 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0),
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
                                                   return new(1, 6, 6);
                                               }

                                               if (location == newLocation)
                                               {
                                                   return centerSector;
                                               }

                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(movingPlayerId));
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
            {
                Spatial = new()
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
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenSectorOverlapsOldRadius_ShouldStillResyncAllSectorsInNewRadius()
    {
        var movingPlayerId = (Serial)0x00003001u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(7 << MapSectorConsts.SectorShift, 7 << MapSectorConsts.SectorShift, 0);
        var newLocation = new Point3D(8 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0);
        var newCenterSector = new MapSector(1, 8, 8);
        var overlappingFarSector = new MapSector(1, 6, 6); // overlap with old radius, distance > 1 from new center

        overlappingFarSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000030u,
                Name = "overlap-far-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new(6 << MapSectorConsts.SectorShift, 6 << MapSectorConsts.SectorShift, 0),
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, 8, 8)] = newCenterSector;
        spatial.SectorsByCoordinate[(1, 6, 6)] = overlappingFarSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               if (location == oldLocation)
                                               {
                                                   return new(1, 7, 7);
                                               }

                                               if (location == newLocation)
                                               {
                                                   return newCenterSector;
                                               }

                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(movingPlayerId));
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
            {
                Spatial = new()
                {
                    SectorEnterSyncRadius = 2
                }
            }
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                1,
                1,
                oldLocation,
                newLocation
            )
        );

        var packets = DequeueAll(queue);

        Assert.That(
            packets.Any(
                packet => packet.Packet is ObjectInformationPacket objectInfoPacket &&
                          objectInfoPacket.Serial == (Serial)0x40000030u
            ),
            Is.True
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenMapChanges_ShouldSendMapChangeToMovingPlayer()
    {
        var movingPlayerId = (Serial)0x00000999u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(1, 7, 8),
            SessionsInRange = []
        };
        var character = CreatePlayer(movingPlayerId);
        character.MapId = 1;
        var characterService = new MobileHandlerTestCharacterService(character);
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                0,
                1,
                new(200, 200, 0),
                new(210, 210, 0)
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets, Has.Count.EqualTo(3));
                Assert.That(packets.All(packet => packet.SessionId == movingSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<GeneralInformationPacket>());
                Assert.That(packets[1].Packet, Is.TypeOf<DrawPlayerPacket>());
                Assert.That(packets[2].Packet, Is.TypeOf<ServerChangePacket>());
            }
        );
    }

    [Test]
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenMapChangesAndDestinationSectorMissing_ShouldStillSendMapChangeToMovingPlayer()
    {
        var movingPlayerId = (Serial)0x00000998u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = null,
            SessionsInRange = []
        };
        var character = CreatePlayer(movingPlayerId);
        character.MapId = 1;
        var characterService = new MobileHandlerTestCharacterService(character);
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                0,
                1,
                new(200, 200, 0),
                new(210, 210, 0)
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets, Has.Count.EqualTo(3));
                Assert.That(packets.All(packet => packet.SessionId == movingSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<GeneralInformationPacket>());
                Assert.That(packets[1].Packet, Is.TypeOf<DrawPlayerPacket>());
                Assert.That(packets[2].Packet, Is.TypeOf<ServerChangePacket>());
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForPlayerCharacterLoggedIn_ShouldResolveCharacterFromSessionWithoutPersistence()
    {
        var characterId = (Serial)0x00005000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();

        var character = CreatePlayer(characterId);
        character.Location = new(132, 132, 0);
        character.MapId = 1;

        var session = CreateSession(characterId);
        session.Character = character;
        sessions.Add(session);

        var centerSectorX = character.Location.X >> MapSectorConsts.SectorShift;
        var centerSectorY = character.Location.Y >> MapSectorConsts.SectorShift;
        var centerSector = new MapSector(1, centerSectorX, centerSectorY);
        centerSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000050u,
                Name = "session-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = character.Location,
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, centerSectorX, centerSectorY)] = centerSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var nullCharacterService = new MobileHandlerTestNullCharacterService();
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            nullCharacterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new PlayerCharacterLoggedInEvent(
                session.SessionId,
                (Serial)0x01u,
                characterId
            )
        );

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
    public async Task HandleAsync_ForPlayerCharacterLoggedIn_ShouldSendSectorSnapshotToEnteringPlayer()
    {
        var movingPlayerId = (Serial)0x00004000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

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

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, centerSectorX, centerSectorY)] = centerSector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var sector)
                                                          ? sector
                                                          : null;
                                           };

        var character = CreatePlayer(movingPlayerId);
        character.Location = spawnLocation;
        character.MapId = 1;
        movingSession.Character = character;
        var characterService = new MobileHandlerTestCharacterService(character);
        var speechService = new MobileHandlerTestSpeechService();
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
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

    [Test]
    public async Task HandleAsync_ForPlayerCharacterLoggedIn_ShouldFilterItemsBySessionAccountType()
    {
        var playerId = (Serial)0x00004010u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var regularSession = CreateSession(playerId);
        regularSession.AccountType = AccountType.Regular;
        sessions.Add(regularSession);

        var spawnLocation = new Point3D(132, 132, 0);
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

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, sectorX, sectorY)] = sector;
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (1, location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.SectorsByCoordinate.TryGetValue(key, out var resolved)
                                                          ? resolved
                                                          : null;
                                           };

        var character = CreatePlayer(playerId);
        character.Location = spawnLocation;
        character.MapId = 1;
        regularSession.Character = character;
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(character),
            new MobileHandlerTestSpeechService(),
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(new PlayerCharacterLoggedInEvent(regularSession.SessionId, (Serial)0x01u, playerId));

        var packets = DequeueAll(queue);

        Assert.That(packets.Any(packet => packet.Packet is ObjectInformationPacket), Is.False);
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
