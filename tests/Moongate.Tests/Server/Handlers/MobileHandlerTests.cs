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
using Moongate.UO.Data.Version;

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
        private readonly UOItemEntity? _backpack;

        public MobileHandlerTestCharacterService(UOMobileEntity mobile, UOItemEntity? backpack = null)
        {
            _mobile = mobile;
            _backpack = backpack;
        }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult(character.Id);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(_mobile.Id == character.Id ? _backpack : null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult<UOMobileEntity?>(_mobile.Id == characterId ? _mobile : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class MobileHandlerTestLightService : ILightService
    {
        public int GlobalLightLevel { get; set; } = 7;

        public int ComputeGlobalLightLevel(DateTime? utcNow = null)
        {
            _ = utcNow;

            return GlobalLightLevel;
        }

        public int ComputeGlobalLightLevel(int mapId, Point3D location, DateTime? utcNow = null)
        {
            _ = mapId;
            _ = location;
            _ = utcNow;

            return GlobalLightLevel;
        }

        public void SetGlobalLightOverride(int? lightLevel, bool applyImmediately = true)
        {
            _ = lightLevel;
            _ = applyImmediately;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
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
        public List<UOItemEntity> NearbyItems { get; set; } = [];
        public List<UOMobileEntity> NearbyMobiles { get; set; } = [];
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
            GetSectorByLocationCallCount++;
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
    public async Task HandleAsync_ForMobileAppearanceChanged_ShouldRefreshChangedPlayerAndRedrawNearbyObservers()
    {
        var playerId = (Serial)0x00000071u;
        var observerId = (Serial)0x00000072u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var playerSession = CreateSession(playerId);
        var observerSession = CreateSession(observerId);
        sessions.Add(playerSession);
        sessions.Add(observerSession);

        var changedPlayer = CreatePlayer(playerId);
        changedPlayer.Location = new(150, 160, 0);
        changedPlayer.MapId = 1;
        changedPlayer.AddEquippedItem(
            ItemLayerType.OuterTorso,
            new UOItemEntity
            {
                Id = (Serial)0x40000071u,
                ItemId = 0x204E,
                Name = "Death Shroud",
                Hue = 0x0021
            }
        );
        playerSession.Character = changedPlayer;

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SessionsInRange = [playerSession, observerSession]
        };
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(changedPlayer),
            new MobileHandlerTestSpeechService(),
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(new MobileAppearanceChangedEvent(changedPlayer));

        var packets = DequeueAll(queue);
        var selfPackets = packets.Where(packet => packet.SessionId == playerSession.SessionId).ToList();
        var observerPackets = packets.Where(packet => packet.SessionId == observerSession.SessionId).ToList();

        Assert.Multiple(
            () =>
            {
                Assert.That(selfPackets.Any(packet => packet.Packet is DrawPlayerPacket), Is.True);
                Assert.That(selfPackets.Any(packet => packet.Packet is MobileDrawPacket), Is.True);
                Assert.That(selfPackets.Any(packet => packet.Packet is WornItemPacket), Is.True);
                Assert.That(selfPackets.Any(packet => packet.Packet is PlayerStatusPacket), Is.True);
                Assert.That(selfPackets.Any(packet => packet.Packet is MobileMovingPacket), Is.False);
                Assert.That(observerPackets.Any(packet => packet.Packet is MobileIncomingPacket), Is.True);
                Assert.That(observerPackets.Any(packet => packet.Packet is WornItemPacket), Is.True);
                Assert.That(observerPackets.Any(packet => packet.Packet is PlayerStatusPacket), Is.True);
                Assert.That(observerPackets.Any(packet => packet.Packet is MobileMovingPacket), Is.False);
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobileAppearanceChanged_WhenPlayerSessionExists_ShouldUpdateSessionCharacterReference()
    {
        var playerId = (Serial)0x00000073u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var playerSession = CreateSession(playerId);
        sessions.Add(playerSession);

        var staleCharacter = CreatePlayer(playerId);
        var changedPlayer = CreatePlayer(playerId);
        changedPlayer.Location = new(220, 221, 0);
        changedPlayer.MapId = 1;
        playerSession.Character = staleCharacter;

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SessionsInRange = [playerSession]
        };
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(changedPlayer),
            new MobileHandlerTestSpeechService(),
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(new MobileAppearanceChangedEvent(changedPlayer));

        Assert.That(playerSession.Character, Is.SameAs(changedPlayer));
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_ShouldNotSendMountedCreaturesAsStandaloneMobiles()
    {
        var movingPlayerId = (Serial)0x00003010u;
        var mountedCreatureId = (Serial)0x00003011u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(100, 100, 0);
        var newLocation = new Point3D(132, 132, 0);
        var centerSector = new MapSector(1, 8, 8);
        centerSector.AddEntity(
            new UOMobileEntity
            {
                Id = mountedCreatureId,
                Name = "mounted-horse",
                IsPlayer = false,
                Location = newLocation,
                MapId = 1,
                RiderMobileId = (Serial)0x00003012u
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, 8, 8)] = centerSector;
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

                                               return null;
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

        Assert.That(packets.Any(packet => packet.Packet is MobileIncomingPacket), Is.False);
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
        movingSession.NetworkSession.SetClientType(ClientType.SA);
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
    public async Task HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldNotSendMobilesOutsidePlayerViewRange()
    {
        var movingPlayerId = (Serial)0x0000B100u;
        var farNpcId = (Serial)0x0000B200u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        movingSession.ViewRange = 5;
        sessions.Add(movingSession);

        var oldLocation = new Point3D(100, 100, 0);
        var newLocation = new Point3D(132, 132, 0);
        var centerSector = new MapSector(1, 8, 8);
        centerSector.AddEntity(
            new UOMobileEntity
            {
                Id = farNpcId,
                IsPlayer = false,
                Name = "far-guard",
                Location = new(145, 132, 0),
                MapId = 1,
                BaseBody = 0x0190
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocationResolver = (_, location) => location == oldLocation ? new(1, 6, 6) : centerSector
        };
        var characterService = new MobileHandlerTestCharacterService(
            new()
            {
                Id = movingPlayerId,
                IsPlayer = true,
                Name = "player",
                Location = newLocation,
                MapId = 1
            }
        );
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

        Assert.That(packets.Any(packet => packet.Packet is MobileIncomingPacket), Is.False);
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldReuseLoadedSectorsForSnapshot()
    {
        var movingPlayerId = (Serial)0x00003003u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        movingSession.NetworkSession.SetClientType(ClientType.SA);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(7 << MapSectorConsts.SectorShift, 7 << MapSectorConsts.SectorShift, 0);
        var newLocation = new Point3D(8 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0);
        var centerSector = new MapSector(1, 8, 8);
        var eastSector = new MapSector(1, 9, 8);
        centerSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000032u,
                Name = "center-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = newLocation,
                MapId = 1
            }
        );
        eastSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000033u,
                Name = "east-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new(9 << MapSectorConsts.SectorShift, 8 << MapSectorConsts.SectorShift, 0),
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();

        for (var sectorX = 7; sectorX <= 9; sectorX++)
        {
            for (var sectorY = 7; sectorY <= 9; sectorY++)
            {
                spatial.ActiveSectors.Add(new(1, sectorX, sectorY));
            }
        }

        spatial.ActiveSectors.RemoveAll(sector => sector.SectorX == 8 && sector.SectorY == 8);
        spatial.ActiveSectors.RemoveAll(sector => sector.SectorX == 9 && sector.SectorY == 8);
        spatial.ActiveSectors.Add(centerSector);
        spatial.ActiveSectors.Add(eastSector);
        spatial.SectorByLocationResolver = (_, location) =>
                                           {
                                               var key = (location.X >> MapSectorConsts.SectorShift,
                                                          location.Y >> MapSectorConsts.SectorShift);

                                               return spatial.ActiveSectors.FirstOrDefault(
                                                   sector => sector.MapIndex == 1 &&
                                                             sector.SectorX == key.Item1 &&
                                                             sector.SectorY == key.Item2
                                               );
                                           };

        var characterService = new MobileHandlerTestCharacterService(CreatePlayer(movingPlayerId));
        var handler = new MobileHandler(
            spatial,
            characterService,
            new MobileHandlerTestSpeechService(),
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

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.GetSectorByLocationCallCount, Is.EqualTo(1));
                Assert.That(
                    packets.Count(packet => packet.Packet is ObjectInformationPacket),
                    Is.GreaterThanOrEqualTo(2)
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
        movingSession.NetworkSession.SetClientType(ClientType.SA);
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
        movingSession.NetworkSession.SetClientType(ClientType.SA);
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
        HandleAsync_ForMobilePositionChanged_WhenEnteringNewSector_ShouldTreatSectorSnapshotMobilesAsNewIncoming()
    {
        var movingPlayerId = (Serial)0x0000A100u;
        var npcId = (Serial)0x0000A200u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        movingSession.NetworkSession.SetClientType(ClientType.SA);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(100, 100, 0);
        var newLocation = new Point3D(132, 132, 0);
        var newSector = new MapSector(1, 8, 8);
        newSector.AddEntity(
            new UOMobileEntity
            {
                Id = npcId,
                IsPlayer = false,
                Name = "guard",
                Location = newLocation,
                MapId = 1,
                BaseBody = 0x0190
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocationResolver = (_, location) => location == oldLocation ? new(1, 6, 6) : newSector
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

        var mobileIncomingPackets = DequeueAll(queue)
                                    .Select(packet => packet.Packet)
                                    .OfType<MobileIncomingPacket>()
                                    .ToList();

        Assert.That(mobileIncomingPackets, Is.Not.Empty);
        Assert.That(mobileIncomingPackets.All(packet => packet.NewMobileIncoming), Is.True);
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenMapChanges_ShouldDeleteOldRangeEntitiesBeforeResync()
    {
        var movingPlayerId = (Serial)0x00000995u;
        var oldNpcId = (Serial)0x00002001u;
        var oldItemId = (Serial)0x40002001u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(2, 47, 17),
            SessionsInRange = [],
            NearbyMobiles =
            [
                new()
                {
                    Id = movingPlayerId,
                    IsPlayer = true,
                    Location = new(100, 100, 0),
                    MapId = 1
                },
                new()
                {
                    Id = oldNpcId,
                    IsPlayer = false,
                    Location = new(101, 100, 0),
                    MapId = 1
                }
            ],
            NearbyItems =
            [
                new()
                {
                    Id = oldItemId,
                    ItemId = 0x0EED,
                    Location = new(102, 100, 0),
                    MapId = 1
                }
            ]
        };

        var character = CreatePlayer(movingPlayerId);
        character.MapId = 2;
        character.Location = new(1518, 568, -14);
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(character),
            new MobileHandlerTestSpeechService(),
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
                2,
                new(100, 100, 0),
                new(1518, 568, -14)
            )
        );

        var packets = DequeueAll(queue)
                      .Where(packet => packet.SessionId == movingSession.SessionId)
                      .ToList();
        var deletePackets = packets.Select(static packet => packet.Packet)
                                   .OfType<DeleteObjectPacket>()
                                   .ToList();
        var firstDeleteIndex = packets.FindIndex(packet => packet.Packet is DeleteObjectPacket);
        var serverChangeIndex = packets.FindIndex(packet => packet.Packet is ServerChangePacket);

        Assert.Multiple(
            () =>
            {
                Assert.That(deletePackets.Select(static packet => packet.Serial), Does.Contain(oldNpcId));
                Assert.That(deletePackets.Select(static packet => packet.Serial), Does.Contain(oldItemId));
                Assert.That(deletePackets.Select(static packet => packet.Serial), Does.Not.Contain(movingPlayerId));
                Assert.That(packets[0].Packet, Is.TypeOf<GeneralInformationPacket>());
                Assert.That(serverChangeIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(firstDeleteIndex, Is.GreaterThan(serverChangeIndex));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenMapChanges_ShouldPreferSessionCharacterOverStalePersistence()
    {
        var movingPlayerId = (Serial)0x00000997u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        movingSession.Character = new()
        {
            Id = movingPlayerId,
            IsPlayer = true,
            Name = "live-player",
            MapId = 2,
            Location = new(1518, 568, -14)
        };
        sessions.Add(movingSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(2, 47, 17),
            SessionsInRange = []
        };
        var staleCharacter = CreatePlayer(movingPlayerId);
        staleCharacter.MapId = 1;
        staleCharacter.Location = new(100, 100, 0);
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(staleCharacter),
            new MobileHandlerTestSpeechService(),
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
                2,
                new(100, 100, 0),
                new(1518, 568, -14)
            )
        );

        var packets = DequeueAll(queue);
        var drawPlayerPacket = packets.OfType<OutgoingGamePacket>()
                                      .Select(static packet => packet.Packet)
                                      .OfType<DrawPlayerPacket>()
                                      .Single();
        var serverChangePacket = packets.OfType<OutgoingGamePacket>()
                                        .Select(static packet => packet.Packet)
                                        .OfType<ServerChangePacket>()
                                        .Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(drawPlayerPacket.Mobile, Is.SameAs(movingSession.Character));
                Assert.That(drawPlayerPacket.Mobile!.Location, Is.EqualTo(new Point3D(1518, 568, -14)));
                Assert.That(serverChangePacket.Location, Is.EqualTo(new Point3D(1518, 568, -14)));
            }
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
                Assert.That(packets.All(packet => packet.SessionId == movingSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<GeneralInformationPacket>());
                Assert.That(packets.Any(packet => packet.Packet is DrawPlayerPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is MobileDrawPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PlayerStatusPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is OverallLightLevelPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PersonalLightLevelPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is SeasonPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PaperdollPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is SetMusicPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is ServerChangePacket), Is.True);
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenMapChanges_ShouldSendWornItemsBackpackAndLightToMovingPlayer()
    {
        var movingPlayerId = (Serial)0x00000996u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(2, 47, 17),
            SessionsInRange = []
        };

        var character = CreatePlayer(movingPlayerId);
        character.MapId = 2;
        character.Location = new(1518, 568, -14);
        character.AddEquippedItem(
            ItemLayerType.Helm,
            new UOItemEntity
            {
                Id = (Serial)0x40001000u,
                ItemId = 0x140A,
                Hue = 0x0481
            }
        );

        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40001001u,
            ItemId = 0x0E75,
            Name = "Backpack"
        };
        backpack.AddItem(
            new UOItemEntity
            {
                Id = (Serial)0x40001002u,
                ItemId = 0x0EED,
                Amount = 10,
                Name = "Gold"
            },
            new(1, 1)
        );
        character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        character.BackpackId = backpack.Id;

        var characterService = new MobileHandlerTestCharacterService(character, backpack);
        var speechService = new MobileHandlerTestSpeechService();
        var lightService = new MobileHandlerTestLightService { GlobalLightLevel = 9 };
        var handler = new MobileHandler(
            spatial,
            characterService,
            speechService,
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new(),
            lightService
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                movingSession.SessionId,
                movingPlayerId,
                1,
                2,
                new(100, 100, 0),
                new(1518, 568, -14)
            )
        );

        var packets = DequeueAll(queue);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets.Any(packet => packet.Packet is WornItemPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is DrawContainerAndAddItemCombinedPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is OverallLightLevelPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PersonalLightLevelPacket), Is.True);
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
                Assert.That(packets.All(packet => packet.SessionId == movingSession.SessionId), Is.True);
                Assert.That(packets[0].Packet, Is.TypeOf<GeneralInformationPacket>());
                Assert.That(packets.Any(packet => packet.Packet is DrawPlayerPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is MobileDrawPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PlayerStatusPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is OverallLightLevelPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PersonalLightLevelPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is SeasonPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is PaperdollPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is SetMusicPacket), Is.True);
                Assert.That(packets.Any(packet => packet.Packet is ServerChangePacket), Is.True);
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenMarkedAsTeleport_ShouldSendTeleportEffectsAndSound()
    {
        var movingPlayerId = (Serial)0x00007000u;
        var observerId = (Serial)0x00007001u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        var observerSession = CreateSession(observerId);
        sessions.Add(movingSession);
        sessions.Add(observerSession);

        var character = CreatePlayer(movingPlayerId);
        character.Location = new(420, 420, 0);
        character.MapId = 1;
        movingSession.Character = character;

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(1, 26, 26),
            SessionsInRange = [movingSession, observerSession]
        };
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(character),
            new MobileHandlerTestSpeechService(),
            new DispatchEventsService(spatial, queue, sessions),
            sessions,
            queue,
            new()
        );

        await handler.HandleAsync(
            new MobilePositionChangedEvent(
                99,
                movingPlayerId,
                1,
                1,
                new(400, 400, 0),
                new(420, 420, 0),
                true
            )
        );

        var packets = DequeueAll(queue);
        var particlePackets = packets.Where(packet => packet.Packet is ParticleEffectPacket)
                                     .Select(
                                         packet => (OutgoingGamePacket: packet, Packet: (ParticleEffectPacket)packet.Packet)
                                     )
                                     .ToList();
        var soundPackets = packets.Where(packet => packet.Packet is PlaySoundEffectPacket)
                                  .Select(
                                      packet => (OutgoingGamePacket: packet, Packet: (PlaySoundEffectPacket)packet.Packet)
                                  )
                                  .ToList();

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    particlePackets.Any(
                        entry =>
                            entry.OutgoingGamePacket.SessionId == movingSession.SessionId &&
                            entry.Packet.SourceLocation == new Point3D(400, 400, 0) &&
                            entry.Packet.Effect == 2023
                    ),
                    Is.True
                );
                Assert.That(
                    particlePackets.Any(
                        entry =>
                            entry.OutgoingGamePacket.SessionId == movingSession.SessionId &&
                            entry.Packet.SourceLocation == new Point3D(420, 420, 0) &&
                            entry.Packet.Effect == 5023
                    ),
                    Is.True
                );
                Assert.That(
                    soundPackets.Any(
                        entry =>
                            entry.OutgoingGamePacket.SessionId == movingSession.SessionId &&
                            entry.Packet.Location == new Point3D(420, 420, 0) &&
                            entry.Packet.SoundModel == 0x01FE
                    ),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenNearSectorEdge_ShouldNotSendPreloadItemsOutsideViewRange()
    {
        var movingPlayerId = (Serial)0x00003002u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var sectorX = 8;
        var sectorY = 8;
        var sectorBaseX = sectorX << MapSectorConsts.SectorShift;
        var sectorBaseY = sectorY << MapSectorConsts.SectorShift;

        var oldLocation = new Point3D(sectorBaseX + 10, sectorBaseY + 8, 0);
        var newLocation = new Point3D(sectorBaseX + 13, sectorBaseY + 8, 0);
        var currentSector = new MapSector(1, sectorX, sectorY);
        var eastSector = new MapSector(1, sectorX + 2, sectorY);

        eastSector.AddEntity(
            new UOItemEntity
            {
                Id = (Serial)0x40000031u,
                Name = "east-preload-item",
                ItemId = 0x0EED,
                ParentContainerId = Serial.Zero,
                EquippedMobileId = Serial.Zero,
                Location = new((sectorX + 2) << MapSectorConsts.SectorShift, sectorBaseY + 8, 0),
                MapId = 1
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService();
        spatial.SectorsByCoordinate[(1, sectorX, sectorY)] = currentSector;
        spatial.SectorsByCoordinate[(1, sectorX + 2, sectorY)] = eastSector;
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

        Assert.That(
            packets.Any(
                packet => packet.Packet is ObjectInformationPacket objectInfoPacket &&
                          objectInfoPacket.Serial == (Serial)0x40000031u
            ),
            Is.False
        );
    }

    [Test]
    public async Task HandleAsync_ForMobilePositionChanged_WhenRecipientIsClassicClient_ShouldUseLegacyMobileIncomingFormat()
    {
        var movingPlayerId = (Serial)0x00003000u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        movingSession.NetworkSession.SetClientType(ClientType.Classic);
        sessions.Add(movingSession);

        var oldLocation = new Point3D(100, 100, 0);
        var newLocation = new Point3D(132, 132, 0);
        var newSector = new MapSector(1, 8, 8);
        newSector.AddEntity(
            new UOMobileEntity
            {
                Id = (Serial)0x00009999u,
                IsPlayer = false,
                Name = "guard",
                Location = newLocation,
                MapId = 1,
                BaseBody = 0x0190
            }
        );

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocationResolver = (_, location) => location == oldLocation ? new(1, 6, 6) : newSector
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
            new MobilePositionChangedEvent(movingSession.SessionId, movingPlayerId, 1, 1, oldLocation, newLocation)
        );

        var mobileIncomingPackets = DequeueAll(queue)
                                    .Select(packet => packet.Packet)
                                    .OfType<MobileIncomingPacket>()
                                    .ToList();

        Assert.That(mobileIncomingPackets, Is.Not.Empty);
        Assert.That(mobileIncomingPackets.All(packet => packet.NewMobileIncoming), Is.False);
    }

    [Test]
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenSameMapTeleport_ShouldRefreshMovingPlayerBeforeDestinationEffect()
    {
        var movingPlayerId = (Serial)0x00007010u;
        var observerId = (Serial)0x00007011u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        var observerSession = CreateSession(observerId);
        sessions.Add(movingSession);
        sessions.Add(observerSession);

        var character = CreatePlayer(movingPlayerId);
        character.Location = new(420, 420, 0);
        character.MapId = 1;
        movingSession.Character = character;

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(1, 26, 26),
            SessionsInRange = [movingSession, observerSession]
        };
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(character),
            new MobileHandlerTestSpeechService(),
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
                new(400, 400, 0),
                new(420, 420, 0),
                true
            )
        );

        var packets = DequeueAll(queue)
                      .Where(packet => packet.SessionId == movingSession.SessionId)
                      .ToList();
        var drawPlayerIndex = packets.FindIndex(packet => packet.Packet is DrawPlayerPacket);
        var mobileDrawIndex = packets.FindIndex(packet => packet.Packet is MobileDrawPacket);
        var destinationEffectIndex = packets.FindIndex(
            packet => packet.Packet is ParticleEffectPacket particleEffectPacket &&
                      particleEffectPacket.SourceLocation == new Point3D(420, 420, 0) &&
                      particleEffectPacket.Effect == 5023
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(drawPlayerIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(mobileDrawIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(destinationEffectIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(drawPlayerIndex, Is.LessThan(destinationEffectIndex));
                Assert.That(mobileDrawIndex, Is.LessThan(destinationEffectIndex));
            }
        );
    }

    [Test]
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenSectorOverlapsOldRadius_ShouldNotResyncItemsOutsideViewRange()
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
            Is.False
        );
    }

    [Test]
    public async Task
        HandleAsync_ForMobilePositionChanged_WhenTeleportChangesMap_ShouldNotSendEffectsBeforeServerChangeToMovingPlayer()
    {
        var movingPlayerId = (Serial)0x00000994u;
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessions = new FakeGameNetworkSessionService();
        var movingSession = CreateSession(movingPlayerId);
        sessions.Add(movingSession);

        var spatial = new MobileHandlerTestSpatialWorldService
        {
            SectorByLocation = new(2, 47, 17),
            SessionsInRange = [movingSession]
        };

        var character = CreatePlayer(movingPlayerId);
        character.MapId = 2;
        character.Location = new(1518, 568, -14);
        var handler = new MobileHandler(
            spatial,
            new MobileHandlerTestCharacterService(character),
            new MobileHandlerTestSpeechService(),
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
                2,
                new(100, 100, 0),
                new(1518, 568, -14),
                true
            )
        );

        var packets = DequeueAll(queue)
                      .Where(packet => packet.SessionId == movingSession.SessionId)
                      .ToList();
        var serverChangeIndex = packets.FindIndex(packet => packet.Packet is ServerChangePacket);

        Assert.That(serverChangeIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(
            packets.Take(serverChangeIndex)
                   .Any(packet => packet.Packet is ParticleEffectPacket or PlaySoundEffectPacket),
            Is.False
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
