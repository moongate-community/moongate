using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class CombatModuleTests
{
    private sealed class RecordingCombatService : ICombatService
    {
        public Serial LastAttackerId { get; private set; }
        public Serial LastDefenderId { get; private set; }
        public int ClearCalls { get; private set; }

        public Task<bool> ClearCombatantAsync(Serial attackerId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastAttackerId = attackerId;
            ClearCalls++;

            return Task.FromResult(true);
        }

        public Task<bool> TrySetCombatantAsync(
            Serial attackerId,
            Serial defenderId,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastAttackerId = attackerId;
            LastDefenderId = defenderId;

            return Task.FromResult(true);
        }
    }

    private sealed class CombatTestGameEventBusService : IGameEventBusService
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

    private sealed class CombatTestSpatialWorldService : ISpatialWorldService
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
    public void SetTarget_AndClearTarget_ShouldDelegateToCombatService()
    {
        var spatial = new CombatTestSpatialWorldService();
        var npc = new UOMobileEntity { Id = (Serial)0x401u, MapId = 1, Location = new(100, 100, 0) };
        var target = new UOMobileEntity { Id = (Serial)0x402u, IsPlayer = true, MapId = 1, Location = new(101, 100, 0) };
        spatial.AddMobile(npc);
        spatial.AddMobile(target);
        var combatService = new RecordingCombatService();
        var module = new CombatModule(spatial, new CombatTestGameEventBusService(), combatService);

        var set = module.SetTarget((uint)npc.Id, (uint)target.Id);
        var cleared = module.ClearTarget((uint)npc.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(set, Is.True);
                Assert.That(cleared, Is.True);
                Assert.That(combatService.LastAttackerId, Is.EqualTo(npc.Id));
                Assert.That(combatService.LastDefenderId, Is.EqualTo(target.Id));
                Assert.That(combatService.ClearCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void Swing_WhenTargetInRange_ShouldPublishAnimationEvent()
    {
        Body.Types = new UOBodyType[10];
        Body.Types[1] = UOBodyType.Human;

        var spatial = new CombatTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x411u,
            MapId = 1,
            Location = new(100, 100, 0),
            BaseBody = new Body(1)
        };
        var target = new UOMobileEntity { Id = (Serial)0x412u, IsPlayer = true, MapId = 1, Location = new(101, 100, 0) };
        spatial.AddMobile(npc);
        spatial.AddMobile(target);
        var eventBus = new CombatTestGameEventBusService();
        var module = new CombatModule(spatial, eventBus, new RecordingCombatService());

        var swung = module.Swing((uint)npc.Id, (uint)target.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(swung, Is.True);
                Assert.That(eventBus.PublishedEvents.Any(gameEvent => gameEvent is MobilePlayAnimationEvent), Is.True);
            }
        );
    }
}
