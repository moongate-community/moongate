using System.Collections.Concurrent;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services;

public class MapSectorSystem
{

    /// <summary>
    /// All sectors indexed by map and coordinates
    /// </summary>
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<(int x, int y), MapSector>> _mapSectors = new();

    /// <summary>
    /// Quick lookup for entities by serial
    /// </summary>
    private readonly ConcurrentDictionary<Serial, (int mapIndex, int sectorX, int sectorY)> _entityLocations = new();


    public MapSectorSystem()
    {
        /// Initialize sectors for all known maps
        InitializeMapsectors();
    }

    private void InitializeMapsectors()
    {
        /// Initialize sectors for standard UO maps
        for (int mapIndex = 0; mapIndex < 6; mapIndex++)
        {
            _mapSectors[mapIndex] = new ConcurrentDictionary<(int x, int y), MapSector>();
        }
    }


    /// <summary>
    /// Adds an entity to the spatial index
    /// </summary>
    public void AddEntity(IPositionEntity entity, int mapIndex)
    {
        if (entity?.Location == null) return;

        var (sectorX, sectorY) = GetSectorCoordinates(entity.Location);
        var sector = GetOrCreateSector(mapIndex, sectorX, sectorY);

        sector.AddEntity(entity);

        /// Update quick lookup
        var serial = GetEntitySerial(entity);
        if (serial != Serial.MinusOne)
        {
            _entityLocations[serial] = (mapIndex, sectorX, sectorY);
        }
    }

    /// <summary>
    /// Removes an entity from the spatial index
    /// </summary>
    public void RemoveEntity(IPositionEntity entity, int mapIndex)
    {
        if (entity?.Location == null) return;

        var serial = GetEntitySerial(entity);
        if (serial == Serial.MinusOne) return;

        if (_entityLocations.TryRemove(serial, out var location))
        {
            var sector = GetSector(location.mapIndex, location.sectorX, location.sectorY);
            sector?.RemoveEntity(entity);
        }
    }

    /// <summary>
    /// Moves an entity to a new location
    /// </summary>
    public void MoveEntity(IPositionEntity entity, int mapIndex, Point3D oldLocation, Point3D newLocation)
    {
        var oldSector = GetSectorCoordinates(oldLocation);
        var newSector = GetSectorCoordinates(newLocation);

        /// If same sector, no need to move between sectors
        if (oldSector == newSector) return;

        /// Remove from old sector
        var oldSectorObj = GetSector(mapIndex, oldSector.x, oldSector.y);
        oldSectorObj?.RemoveEntity(entity);

        /// Add to new sector
        var newSectorObj = GetOrCreateSector(mapIndex, newSector.x, newSector.y);
        newSectorObj.AddEntity(entity);

        /// Update lookup
        var serial = GetEntitySerial(entity);
        if (serial != Serial.MinusOne)
        {
            _entityLocations[serial] = (mapIndex, newSector.x, newSector.y);
        }
    }

    /// <summary>
    /// Gets all entities within range of a point
    /// </summary>
    public List<T> GetEntitiesInRange<T>(Point3D center, int range, int mapIndex) where T : class, IPositionEntity
    {
        var results = new List<T>();
        var sectorsToCheck = GetSectorsInRange(center, range);

        foreach (var (sectorX, sectorY) in sectorsToCheck)
        {
            var sector = GetSector(mapIndex, sectorX, sectorY);
            if (sector == null) continue;

            var entities = sector.GetEntitiesInRange<T>(center, range);
            results.AddRange(entities);
        }

        return results;
    }

    /// <summary>
    /// Gets all mobiles within view range of a player
    /// </summary>
    public List<UOMobileEntity> GetMobilesInViewRange(
        Point3D center, int mapIndex, int viewRange = MapSectorConsts.MaxViewRange
    )
    {
        return GetEntitiesInRange<UOMobileEntity>(center, viewRange, mapIndex);
    }

    /// <summary>
    /// Gets all items within range of a point
    /// </summary>
    public List<UOItemEntity> GetItemsInRange(Point3D center, int range, int mapIndex)
    {
        return GetEntitiesInRange<UOItemEntity>(center, range, mapIndex);
    }

    /// <summary>
    /// Gets all players within range (for broadcasting packets)
    /// </summary>
    public List<UOMobileEntity> GetPlayersInRange(Point3D center, int range, int mapIndex)
    {
        return GetEntitiesInRange<UOMobileEntity>(center, range, mapIndex)
            .Where(m => m.IsPlayer)
            .ToList();
    }

    /// <summary>
    /// Fast lookup for a specific entity by serial
    /// </summary>
    public T? FindEntity<T>(Serial serial) where T : class, IPositionEntity
    {
        if (_entityLocations.TryGetValue(serial, out var location))
        {
            var sector = GetSector(location.mapIndex, location.sectorX, location.sectorY);
            return sector?.GetEntity<T>(serial);
        }

        return null;
    }

    /// <summary>
    /// Gets sector coordinates for a world position
    /// </summary>
    private (int x, int y) GetSectorCoordinates(Point3D location)
    {
        /// Fast division using bit shifting
        return (location.X >> MapSectorConsts.SectorShift, location.Y >> MapSectorConsts.SectorShift);
    }

    /// <summary>
    /// Gets all sector coordinates that intersect with a range
    /// </summary>
    private List<(int x, int y)> GetSectorsInRange(Point3D center, int range)
    {
        var sectors = new List<(int x, int y)>();

        /// Calculate bounding box
        var minX = (center.X - range) >> MapSectorConsts.SectorShift;
        var maxX = (center.X + range) >> MapSectorConsts.SectorShift;
        var minY = (center.Y - range) >> MapSectorConsts.SectorShift;
        var maxY = (center.Y + range) >> MapSectorConsts.SectorShift;

        /// Add all sectors in the bounding box
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                sectors.Add((x, y));
            }
        }

        return sectors;
    }

    /// <summary>
    /// Gets an existing sector
    /// </summary>
    private MapSector? GetSector(int mapIndex, int sectorX, int sectorY)
    {
        if (_mapSectors.TryGetValue(mapIndex, out var mapSectors))
        {
            mapSectors.TryGetValue((sectorX, sectorY), out var sector);
            return sector;
        }

        return null;
    }

    /// <summary>
    /// Gets or creates a sector
    /// </summary>
    private MapSector GetOrCreateSector(int mapIndex, int sectorX, int sectorY)
    {
        var mapSectors = _mapSectors.GetOrAdd(mapIndex, _ => new ConcurrentDictionary<(int x, int y), MapSector>());

        return mapSectors.GetOrAdd((sectorX, sectorY), _ => new MapSector(mapIndex, sectorX, sectorY));
    }

    /// <summary>
    /// Extracts serial from an entity
    /// </summary>
    private Serial GetEntitySerial(IPositionEntity entity)
    {
        return entity switch
        {
            UOMobileEntity mobile => mobile.Id,
            UOItemEntity item     => item.Id,
            _                     => Serial.MinusOne
        };
    }

    /// <summary>
    /// Gets statistics about the sector system
    /// </summary>
    public SectorSystemStats GetStats()
    {
        var totalSectors = 0;
        var totalEntities = 0;
        var maxEntitiesPerSector = 0;

        foreach (var mapSectors in _mapSectors.Values)
        {
            totalSectors += mapSectors.Count;

            foreach (var sector in mapSectors.Values)
            {
                var entityCount = sector.EntityCount;
                totalEntities += entityCount;
                maxEntitiesPerSector = Math.Max(maxEntitiesPerSector, entityCount);
            }
        }

        return new SectorSystemStats
        {
            TotalSectors = totalSectors,
            TotalEntities = totalEntities,
            MaxEntitiesPerSector = maxEntitiesPerSector,
            AverageEntitiesPerSector = totalSectors > 0 ? (double)totalEntities / totalSectors : 0
        };
    }


    public void Dispose()
    {
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
