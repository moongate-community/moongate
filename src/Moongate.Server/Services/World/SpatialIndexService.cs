using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Data.Internal.World;
using Moongate.Server.Scripting;
using Serilog;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.World;

/// <summary>
/// Default <see cref="ISpatialIndexService" />: a sparse 16×16 sector grid holding serials, with a
/// reverse index for O(1) relocation. Single game-loop writer — plain dictionaries, no locks.
/// </summary>
public sealed class SpatialIndexService : ISpatialIndexService
{
    // 16-tile sectors: sector coordinates are world coordinates >> 4.
    private const int SectorShift = 4;

    private readonly ILogger _logger = Log.ForContext<SpatialIndexService>();

    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IEntityStore<ItemEntity, Serial> _items;
    private readonly ILoopThread _loopThread;
    private readonly IEventBus _eventBus;
    private readonly Dictionary<(int MapId, int SectorX, int SectorY), Sector> _sectors = [];
    private readonly Dictionary<Serial, (int MapId, int SectorX, int SectorY)> _locations = [];

    public SpatialIndexService(IPersistenceService persistenceService, ILoopThread loopThread, IEventBus eventBus)
    {
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _items = persistenceService.GetStore<ItemEntity, Serial>();
        _loopThread = loopThread;
        _eventBus = eventBus;
    }

    public void AddOrUpdate(MobileEntity mobile)
    {
        LoopGuard.Warn(_loopThread, nameof(SpatialIndexService) + "." + nameof(AddOrUpdate));
        Place(mobile.Id, mobile.MapId, mobile.Position, true);
    }

    public void AddOrUpdate(ItemEntity item)
    {
        LoopGuard.Warn(_loopThread, nameof(SpatialIndexService) + "." + nameof(AddOrUpdate));

        // A contained or equipped item is not in the world: self-correct instead of polluting sectors.
        if (item.ParentContainerId != Serial.Zero || item.EquippedMobileId != Serial.Zero)
        {
            RemoveCore(item.Id);

            return;
        }

        Place(item.Id, item.MapId, item.Position, false);
    }

    public IReadOnlyList<ItemEntity> GetItemsInRange(int mapId, Point3D center, int range)
    {
        LoopGuard.Warn(_loopThread, nameof(SpatialIndexService) + "." + nameof(GetItemsInRange));
        var results = new List<ItemEntity>();

        foreach (var sector in SectorsInRange(mapId, center, range))
        {
            foreach (var serial in sector.Items)
            {
                if (_items.GetById(serial) is { } item && center.InRange(item.Position, range))
                {
                    results.Add(item);
                }
            }
        }

        return results;
    }

    public IReadOnlyList<MobileEntity> GetMobilesInRange(int mapId, Point3D center, int range)
    {
        LoopGuard.Warn(_loopThread, nameof(SpatialIndexService) + "." + nameof(GetMobilesInRange));
        var results = new List<MobileEntity>();

        foreach (var sector in SectorsInRange(mapId, center, range))
        {
            foreach (var serial in sector.Mobiles)
            {
                if (_mobiles.GetById(serial) is { } mobile && center.InRange(mobile.Position, range))
                {
                    results.Add(mobile);
                }
            }
        }

        return results;
    }

    public void Remove(Serial serial)
    {
        LoopGuard.Warn(_loopThread, nameof(SpatialIndexService) + "." + nameof(Remove));
        RemoveCore(serial);
    }

    private void Place(Serial serial, int mapId, Point3D position, bool isMobile)
    {
        var key = (MapId: mapId, SectorX: position.X >> SectorShift, SectorY: position.Y >> SectorShift);

        var moved = false;
        (int MapId, int SectorX, int SectorY) from = default;

        if (_locations.TryGetValue(serial, out var current))
        {
            if (current == key)
            {
                return;
            }

            from = current;
            moved = true;
            RemoveCore(serial); // publishes the "left" event for the old sector, if this is a mobile
        }

        if (!_sectors.TryGetValue(key, out var sector))
        {
            sector = new();
            _sectors[key] = sector;
        }

        if (isMobile)
        {
            sector.Mobiles.Add(serial);
        }
        else
        {
            sector.Items.Add(serial);
        }

        _locations[serial] = key;

        // Sector transitions are a mobile-only, boundary-only signal: never for items, and never for a
        // step that stays inside the same sector (handled by the same-key early return above).
        if (!isMobile)
        {
            return;
        }

        _logger.Debug("Mobile {Mobile} entered sector {MapId}:{SectorX},{SectorY}", serial, key.MapId, key.SectorX, key.SectorY);
        _eventBus.Publish(new MobileEnteredSectorEvent(serial, key.MapId, key.SectorX, key.SectorY));

        if (moved)
        {
            _logger.Debug(
                "Mobile {Mobile} changed sector {FromMapId}:{FromSectorX},{FromSectorY} -> {ToMapId}:{ToSectorX},{ToSectorY}",
                serial,
                from.MapId,
                from.SectorX,
                from.SectorY,
                key.MapId,
                key.SectorX,
                key.SectorY
            );
            _eventBus.Publish(
                new MobileChangedSectorEvent(serial, from.MapId, from.SectorX, from.SectorY, key.MapId, key.SectorX, key.SectorY)
            );
        }
    }

    private void RemoveCore(Serial serial)
    {
        if (!_locations.Remove(serial, out var key))
        {
            return;
        }

        if (_sectors.TryGetValue(key, out var sector))
        {
            // HashSet.Remove returns whether the serial was present, which is exactly "was this a mobile in
            // this sector" — the gate for emitting the mobile-only "left" signal.
            var wasMobile = sector.Mobiles.Remove(serial);
            sector.Items.Remove(serial);

            if (sector.IsEmpty)
            {
                _sectors.Remove(key);
            }

            if (wasMobile)
            {
                _logger.Debug("Mobile {Mobile} left sector {MapId}:{SectorX},{SectorY}", serial, key.MapId, key.SectorX, key.SectorY);
                _eventBus.Publish(new MobileLeftSectorEvent(serial, key.MapId, key.SectorX, key.SectorY));
            }
        }
    }

    private IEnumerable<Sector> SectorsInRange(int mapId, Point3D center, int range)
    {
        var minX = (center.X - range) >> SectorShift;
        var maxX = (center.X + range) >> SectorShift;
        var minY = (center.Y - range) >> SectorShift;
        var maxY = (center.Y + range) >> SectorShift;

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (_sectors.TryGetValue((mapId, x, y), out var sector))
                {
                    yield return sector;
                }
            }
        }
    }
}
