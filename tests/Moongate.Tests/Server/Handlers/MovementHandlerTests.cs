using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Movement;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Movement;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.World;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Movement;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Handlers;

public class MovementHandlerTests
{
    private sealed class TestMovementTileQueryService : IMovementTileQueryService
    {
        public bool HasMapBounds { get; set; }

        public int Width { get; set; } = 6144;

        public int Height { get; set; } = 4096;

        public Dictionary<(int X, int Y), LandTile> LandTiles { get; } = [];

        public Dictionary<(int X, int Y), List<StaticTile>> StaticTiles { get; } = [];

        public IReadOnlyList<StaticTile> GetStaticTiles(int mapId, int x, int y)
            => StaticTiles.TryGetValue((x, y), out var configured) ? configured : Array.Empty<StaticTile>();

        public bool TryGetLandTile(int mapId, int x, int y, out LandTile landTile)
        {
            if (LandTiles.TryGetValue((x, y), out var configured))
            {
                landTile = configured;

                return true;
            }

            landTile = new(0, 0);

            return true;
        }

        public bool TryGetMapBounds(int mapId, out int width, out int height)
        {
            width = Width;
            height = Height;

            return HasMapBounds;
        }
    }

    private sealed class TestMovementSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> GetNearbyMobilesResult { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => throw new NotImplementedException();

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => throw new NotImplementedException();

        public void AddRegion(JsonRegion region)
            => throw new NotImplementedException();

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => throw new NotImplementedException();

        public List<MapSector> GetActiveSectors()
            => throw new NotImplementedException();

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
            => throw new NotImplementedException();

        public int GetMusic(int mapId, Point3D location)
            => throw new NotImplementedException();

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => throw new NotImplementedException();

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => GetNearbyMobilesResult;

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
            => throw new NotImplementedException();

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => throw new NotImplementedException();

        public JsonRegion? GetRegionById(int regionId)
            => throw new NotImplementedException();

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
            => throw new NotImplementedException();

        public SectorSystemStats GetStats()
            => throw new NotImplementedException();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
            => throw new NotImplementedException();

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
            => throw new NotImplementedException();

        public void RemoveEntity(Serial serial)
            => throw new NotImplementedException();
    }

    private sealed class TestTeleportersDataService : ITeleportersDataService
    {
        public List<TeleporterEntry> Entries { get; } = [];

        public IReadOnlyList<TeleporterEntry> GetAllEntries()
            => Entries;

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceMap(int mapId)
            => [.. Entries.Where(entry => entry.SourceMapId == mapId)];

        public IReadOnlyList<TeleporterEntry> GetEntriesBySourceSector(int mapId, int sectorX, int sectorY)
            => [
                ..Entries.Where(
                      entry =>
                          entry.SourceMapId == mapId &&
                          (entry.SourceLocation.X >> MapSectorConsts.SectorShift) == sectorX &&
                          (entry.SourceLocation.Y >> MapSectorConsts.SectorShift) == sectorY
                  )
            ];

        public bool TryGetEntryAtLocation(int mapId, Point3D location, out TeleporterEntry entry)
        {
            foreach (var candidate in Entries)
            {
                if (candidate.SourceMapId == mapId && candidate.SourceLocation == location)
                {
                    entry = candidate;

                    return true;
                }
            }

            entry = default;

            return false;
        }

        public bool TryResolveTeleportDestination(
            int mapId,
            Point3D location,
            out int destinationMapId,
            out Point3D destinationLocation,
            int maxHops = 4
        )
        {
            _ = maxHops;

            if (TryGetEntryAtLocation(mapId, location, out var entry))
            {
                destinationMapId = entry.DestinationMapId;
                destinationLocation = entry.DestinationLocation;

                return true;
            }

            destinationMapId = mapId;
            destinationLocation = location;

            return false;
        }

        public void SetEntries(IReadOnlyList<TeleporterEntry> entries)
        {
            Entries.Clear();
            Entries.AddRange(entries);
        }
    }

    [Test]
    public async Task HandlePacketAsync_ShouldAckAndAdvanceSequence_WhenSequenceIsValid()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(1200, 1300, 7),
                Direction = DirectionType.East
            }
        };
        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.East | DirectionType.Running,
            Sequence = 0,
            FastWalkKey = 0x55667788
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<MoveConfirmPacket>());
                Assert.That(session.MoveSequence, Is.EqualTo(1));
                Assert.That(session.Character, Is.Not.Null);
                Assert.That(session.Character!.Direction, Is.EqualTo(DirectionType.East | DirectionType.Running));
                Assert.That(session.Character.Location, Is.EqualTo(new Point3D(1201, 1300, 7)));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldApplyAntiSpamForMobilePositionChangedEvent()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(600, 600, 0),
                Direction = DirectionType.East
            }
        };

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );
        _ = queue.TryDequeue(out _);
        session.MoveTime = Environment.TickCount64 - 2000;
        session.MoveCredit = 1000;

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 1
                }
            );

        Assert.That(gameEventBus.Events.OfType<MobilePositionChangedEvent>().ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HandlePacketAsync_ShouldNotThrottleMobilePositionChangedEvent_WhenMapChangesViaTeleporter()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var teleporters = new TestTeleportersDataService();
        teleporters.SetEntries(
            [
                new(
                    0,
                    "Felucca",
                    new(101, 100, 0),
                    1,
                    "Trammel",
                    new(500, 500, 10),
                    false
                )
            ]
        );
        var handler = CreateHandler(queue, gameEventBus, teleportersDataService: teleporters);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                MapId = 0,
                Location = new(100, 100, 0),
                Direction = DirectionType.East
            },
            LastMobilePositionEventTimestamp = Environment.TickCount64
        };

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );
        var events = gameEventBus.Events.OfType<MobilePositionChangedEvent>().ToList();
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].OldMapId, Is.EqualTo(0));
        Assert.That(events[0].MapId, Is.EqualTo(1));
        Assert.That(events[0].NewLocation, Is.EqualTo(new Point3D(500, 500, 10)));
        Assert.That(events[0].IsTeleport, Is.True);
    }

    [Test]
    public async Task HandlePacketAsync_ShouldApplyFasterDelayForRunning()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var walkSession = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            MoveSequence = 0,
            MoveTime = 0,
            IsMounted = false,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(1200, 1300, 7),
                Direction = DirectionType.East
            }
        };
        var runSession = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002,
            MoveSequence = 0,
            MoveTime = 0,
            IsMounted = false,
            Character = new()
            {
                Id = (Serial)0x00000002,
                Location = new(1200, 1300, 7),
                Direction = DirectionType.East
            }
        };

        _ = await handler.HandlePacketAsync(
                walkSession,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );
        _ = queue.TryDequeue(out _);

        _ = await handler.HandlePacketAsync(
                runSession,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East | DirectionType.Running,
                    Sequence = 0
                }
            );
        _ = queue.TryDequeue(out _);

        Assert.That(runSession.MoveTime, Is.LessThan(walkSession.MoveTime - 100));
    }

    [Test]
    public async Task HandlePacketAsync_ShouldDropPacket_WhenFirstSequenceIsNotZero()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            MoveSequence = 0,
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(1200, 1300, 7),
                Direction = DirectionType.East
            }
        };
        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.North,
            Sequence = 4,
            FastWalkKey = 0x11223344
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var dequeued = queue.TryDequeue(out _);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.False);
                Assert.That(session.MoveSequence, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldNotPublishMobilePositionChangedEvent_WhenFacingOnly()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(400, 400, 0),
                Direction = DirectionType.North
            }
        };

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );

        Assert.That(gameEventBus.Events.OfType<MobilePositionChangedEvent>(), Is.Empty);
    }

    [Test]
    public async Task HandlePacketAsync_ShouldOnlyTurnWithoutMoving_WhenFacingChanges()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(500, 500, 0),
                Direction = DirectionType.North
            }
        };

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );
        _ = queue.TryDequeue(out _);

        Assert.Multiple(
            () =>
            {
                Assert.That(session.Character, Is.Not.Null);
                Assert.That(session.Character!.Direction, Is.EqualTo(DirectionType.East));
                Assert.That(session.Character.Location, Is.EqualTo(new Point3D(500, 500, 0)));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishMobilePositionChangedEvent_WhenPositionChanges()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(100, 100, 0),
                Direction = DirectionType.East
            }
        };

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );

        var gameEvent = gameEventBus.Events.OfType<MobilePositionChangedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.MobileId, Is.EqualTo((Serial)0x00000001));
                Assert.That(gameEvent.OldLocation, Is.EqualTo(new Point3D(100, 100, 0)));
                Assert.That(gameEvent.NewLocation, Is.EqualTo(new Point3D(101, 100, 0)));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldTeleport_WhenLocationMatchesTeleporterSource()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var teleporters = new TestTeleportersDataService();
        teleporters.SetEntries(
            [
                new(
                    0,
                    "Felucca",
                    new(101, 100, 0),
                    1,
                    "Trammel",
                    new(500, 500, 10),
                    false
                )
            ]
        );
        var handler = CreateHandler(queue, gameEventBus, teleportersDataService: teleporters);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                MapId = 0,
                Location = new(100, 100, 0),
                Direction = DirectionType.East
            }
        };

        _ = await handler.HandlePacketAsync(
                session,
                new MoveRequestPacket
                {
                    Direction = DirectionType.East,
                    Sequence = 0
                }
            );

        var gameEvent = gameEventBus.Events.OfType<MobilePositionChangedEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(session.Character, Is.Not.Null);
                Assert.That(session.Character!.MapId, Is.EqualTo(1));
                Assert.That(session.Character.Location, Is.EqualTo(new Point3D(500, 500, 10)));
                Assert.That(gameEvent.OldMapId, Is.EqualTo(0));
                Assert.That(gameEvent.MapId, Is.EqualTo(1));
                Assert.That(gameEvent.NewLocation, Is.EqualTo(new Point3D(500, 500, 10)));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldSendDenyAndResetSequence_WhenMoveIsBlockedByMap()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true,
            Width = 64,
            Height = 64
        };
        var handler = CreateHandler(queue, gameEventBus, tileQuery);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            MoveSequence = 7,
            CharacterId = (Serial)0x00000001,
            Character = new()
            {
                Id = (Serial)0x00000001,
                MapId = 1,
                Location = new(0, 0, 0),
                Direction = DirectionType.West
            }
        };

        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.West,
            Sequence = 7
        };

        _ = await handler.HandlePacketAsync(session, packet);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<MoveDenyPacket>());
                Assert.That(session.MoveSequence, Is.EqualTo(0));
                Assert.That(session.Character, Is.Not.Null);
                Assert.That(session.Character!.Location, Is.EqualTo(new Point3D(0, 0, 0)));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldThrottle_WhenMoveTimeIsFarAhead()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var gameEventBus = new NetworkServiceTestGameEventBusService();
        var handler = CreateHandler(queue, gameEventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000001,
            MoveSequence = 1,
            MoveTime = Environment.TickCount64 + 2000,
            MoveCredit = 0,
            Character = new()
            {
                Id = (Serial)0x00000001,
                Location = new(1200, 1300, 7),
                Direction = DirectionType.East
            }
        };

        var packet = new MoveRequestPacket
        {
            Direction = DirectionType.East,
            Sequence = 1,
            FastWalkKey = 0x01020305
        };

        _ = await handler.HandlePacketAsync(session, packet);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<MoveDenyPacket>());
                Assert.That(session.MoveSequence, Is.EqualTo(1));
                var deny = (MoveDenyPacket)outbound.Packet;
                Assert.That(deny.X, Is.EqualTo(1200));
                Assert.That(deny.Y, Is.EqualTo(1300));
                Assert.That(deny.Z, Is.EqualTo(7));
            }
        );
    }

    private static MovementHandler CreateHandler(
        BasePacketListenerTestOutgoingPacketQueue queue,
        NetworkServiceTestGameEventBusService gameEventBus,
        TestMovementTileQueryService? tileQuery = null,
        ISpatialWorldService? spatialWorldService = null,
        ITeleportersDataService? teleportersDataService = null
    )
    {
        var movementValidationService = new MovementValidationService(
            tileQuery ?? new TestMovementTileQueryService(),
            spatialWorldService ?? new TestMovementSpatialWorldService()
        );

        return new(queue, gameEventBus, movementValidationService, teleportersDataService ?? new TestTeleportersDataService());
    }
}
