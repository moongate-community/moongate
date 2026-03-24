using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class SteeringModuleTests
{
    private sealed class SteeringTestGameEventBusService : IGameEventBusService
    {
        public List<IGameEvent> PublishedEvents { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            PublishedEvents.Add(gameEvent);

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;
    }

    private sealed class SteeringTestMovementValidationService : IMovementValidationService
    {
        public bool TryResolveMove(UOMobileEntity mobile, DirectionType direction, out Point3D newLocation)
        {
            var baseDirection = Point3D.GetBaseDirection(direction);
            var dx = 0;
            var dy = 0;

            switch (baseDirection)
            {
                case DirectionType.North:
                    dy = -1;

                    break;
                case DirectionType.NorthEast:
                    dx = 1;
                    dy = -1;

                    break;
                case DirectionType.East:
                    dx = 1;

                    break;
                case DirectionType.SouthEast:
                    dx = 1;
                    dy = 1;

                    break;
                case DirectionType.South:
                    dy = 1;

                    break;
                case DirectionType.SouthWest:
                    dx = -1;
                    dy = 1;

                    break;
                case DirectionType.West:
                    dx = -1;

                    break;
                case DirectionType.NorthWest:
                    dx = -1;
                    dy = -1;

                    break;
            }

            newLocation = new(mobile.Location.X + dx, mobile.Location.Y + dy, mobile.Location.Z);

            return true;
        }
    }

    private sealed class SteeringTestPathfindingService : IPathfindingService
    {
        public bool TryFindPath(
            UOMobileEntity mobile,
            Point3D targetLocation,
            out IReadOnlyList<DirectionType> path,
            int maxVisitedNodes = 1024
        )
        {
            _ = mobile;
            _ = targetLocation;
            _ = maxVisitedNodes;
            path = [DirectionType.East];

            return true;
        }
    }

    private sealed class SteeringTestSessionService : IGameNetworkSessionService
    {
        public int Count => 0;

        public void Clear() { }

        public IReadOnlyCollection<GameSession> GetAll()
            => [];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => false;

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = null!;

            return false;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = null!;

            return false;
        }
    }

    private sealed class SteeringTestSpatialWorldService : ISpatialWorldService
    {
        private readonly List<MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            var sector = new MapSector(
                mobile.MapId,
                mobile.Location.X >> MapSectorConsts.SectorShift,
                mobile.Location.Y >> MapSectorConsts.SectorShift
            );
            sector.AddEntity(mobile);
            _sectors.Add(sector);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddMobile(mobile);

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
            => [.. _sectors];

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
            => [];

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

    [Test]
    public void Follow_ShouldMoveNpcAndPublishMobilePositionChangedEvent()
    {
        var spatial = new SteeringTestSpatialWorldService();
        var npc = new UOMobileEntity { Id = (Serial)0x301u, MapId = 1, Location = new(100, 100, 0) };
        var target = new UOMobileEntity { Id = (Serial)0x302u, MapId = 1, Location = new(105, 100, 0) };
        spatial.AddMobile(npc);
        spatial.AddMobile(target);
        var eventBus = new SteeringTestGameEventBusService();
        var module = new SteeringModule(
            spatial,
            new SteeringTestMovementValidationService(),
            new SteeringTestPathfindingService(),
            eventBus,
            new SteeringTestSessionService()
        );

        var moved = module.Follow((uint)npc.Id, (uint)target.Id, 1);

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.True);
                Assert.That(npc.Location, Is.EqualTo(new Point3D(101, 100, 0)));
                Assert.That(eventBus.PublishedEvents.Any(gameEvent => gameEvent is MobilePositionChangedEvent), Is.True);
            }
        );
    }

    [Test]
    public void MoveTo_ShouldMoveNpcTowardWorldPointAndPublishMobilePositionChangedEvent()
    {
        var spatial = new SteeringTestSpatialWorldService();
        var npc = new UOMobileEntity { Id = (Serial)0x311u, MapId = 1, Location = new(100, 100, 0) };
        spatial.AddMobile(npc);
        var eventBus = new SteeringTestGameEventBusService();
        var module = new SteeringModule(
            spatial,
            new SteeringTestMovementValidationService(),
            new SteeringTestPathfindingService(),
            eventBus,
            new SteeringTestSessionService()
        );

        var moved = module.MoveTo((uint)npc.Id, 105, 100, 0, 1);

        Assert.Multiple(
            () =>
            {
                Assert.That(moved, Is.True);
                Assert.That(npc.Location, Is.EqualTo(new Point3D(101, 100, 0)));
                Assert.That(eventBus.PublishedEvents.Any(gameEvent => gameEvent is MobilePositionChangedEvent), Is.True);
            }
        );
    }
}
