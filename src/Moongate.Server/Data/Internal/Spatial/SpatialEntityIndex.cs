using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Data.Internal.Spatial;

/// <summary>
/// Maintains the spatial entity index and lazy sector loading for entities.
/// </summary>
internal sealed class SpatialEntityIndex
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, SpatialMapIndex> _mapIndices = [];
    private readonly Dictionary<Serial, SpatialEntityLocation> _entityLocations = [];
    private readonly HashSet<(int MapId, int SectorX, int SectorY)> _loadedSectors = [];
    private readonly Dictionary<(int MapId, int SectorX, int SectorY), Task> _sectorLoadTasks = [];
    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;
    private readonly MoongateSpatialConfig _spatialConfig;

    public SpatialEntityIndex(
        IItemService itemService,
        IMobileService mobileService,
        MoongateSpatialConfig spatialConfig
    )
    {
        _itemService = itemService;
        _mobileService = mobileService;
        _spatialConfig = spatialConfig;
    }

    public bool AddOrUpdateMobile(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        var mapId = mobile.MapId;
        var (sectorX, sectorY) = GetSectorCoordinates(mobile.Location);
        EnsureSectorLoaded(mapId, sectorX, sectorY);

        lock (_sync)
        {
            var isNew = !_entityLocations.ContainsKey(mobile.Id);
            RemoveEntityUnsafe(mobile.Id);
            mobile.MapId = mapId;
            var sector = GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
            sector.AddEntity(mobile);
            _entityLocations[mobile.Id] = new() { MapId = mapId, SectorX = sectorX, SectorY = sectorY };

            return isNew;
        }
    }

    public void AddOrUpdateItem(UOItemEntity item, int mapId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (sectorX, sectorY) = GetSectorCoordinates(item.Location);
        EnsureSectorLoaded(mapId, sectorX, sectorY);
        AddOrUpdateItemInternal(item, mapId, sectorX, sectorY);
    }

    public void MoveItem(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Location = newLocation;
        _ = MoveEntity(item.Id, item, mapId, oldLocation, newLocation);
    }

    public SpatialEntityMoveResult MoveMobile(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        mobile.Location = newLocation;
        var mapId = mobile.MapId;
        var (oldSectorX, oldSectorY) = GetSectorCoordinates(oldLocation);
        var (newSectorX, newSectorY) = GetSectorCoordinates(newLocation);
        var sectorChanged = MoveEntity(mobile.Id, mobile, mapId, oldLocation, newLocation);

        return new(
            sectorChanged,
            mapId,
            oldSectorX,
            oldSectorY,
            newSectorX,
            newSectorY
        );
    }

    public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
    {
        var sectors = GetSectorsInRange(location, range);

        foreach (var (x, y) in sectors)
        {
            EnsureSectorLoaded(mapId, x, y);
        }

        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return [];
            }

            var results = new List<UOItemEntity>();

            foreach (var (x, y) in sectors)
            {
                var sector = mapIndex.GetSector(x, y);

                if (sector is not null)
                {
                    results.AddRange(sector.GetEntitiesInRange<UOItemEntity>(location, range));
                }
            }

            return results;
        }
    }

    public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
    {
        var sectors = GetSectorsInRange(location, range);

        foreach (var (x, y) in sectors)
        {
            EnsureSectorLoaded(mapId, x, y);
        }

        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return [];
            }

            var results = new List<UOMobileEntity>();

            foreach (var (x, y) in sectors)
            {
                var sector = mapIndex.GetSector(x, y);

                if (sector is not null)
                {
                    results.AddRange(sector.GetEntitiesInRange<UOMobileEntity>(location, range));
                }
            }

            return results;
        }
    }

    public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
    {
        EnsureSectorLoaded(mapId, sectorX, sectorY);

        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return [];
            }

            var sector = mapIndex.GetSector(sectorX, sectorY);

            if (sector is null)
            {
                return [];
            }

            return [.. sector.GetPlayers()];
        }
    }

    public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
    {
        var clampedRadius = Math.Max(0, radius);
        var sectors = GetSectorCoordinatesInRadius(centerSectorX, centerSectorY, clampedRadius);

        foreach (var (x, y) in sectors)
        {
            EnsureSectorLoaded(mapId, x, y);
        }

        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return [];
            }

            var results = new List<UOMobileEntity>();
            var seen = new HashSet<Serial>();

            foreach (var (x, y) in sectors)
            {
                var sector = mapIndex.GetSector(x, y);

                if (sector is null)
                {
                    continue;
                }

                foreach (var mobile in sector.GetMobiles())
                {
                    if (seen.Add(mobile.Id))
                    {
                        results.Add(mobile);
                    }
                }
            }

            return results;
        }
    }

    public MapSector? GetSectorByLocation(int mapId, Point3D location)
    {
        var (sectorX, sectorY) = GetSectorCoordinates(location);
        EnsureSectorLoaded(mapId, sectorX, sectorY);

        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return null;
            }

            return mapIndex.GetSector(sectorX, sectorY);
        }
    }

    public List<MapSector> GetActiveSectors()
    {
        lock (_sync)
        {
            return [.. _mapIndices.Values.SelectMany(static mapIndex => mapIndex.Sectors)];
        }
    }

    public SectorSystemStats GetStats()
    {
        lock (_sync)
        {
            var allSectors = _mapIndices.Values.SelectMany(static index => index.Sectors).ToList();
            var totalSectors = allSectors.Count;
            var totalEntities = allSectors.Sum(static sector => sector.EntityCount);
            var maxEntitiesPerSector = allSectors.Count == 0 ? 0 : allSectors.Max(static sector => sector.EntityCount);
            var average = totalSectors == 0 ? 0d : (double)totalEntities / totalSectors;

            return new()
            {
                TotalSectors = totalSectors,
                TotalEntities = totalEntities,
                MaxEntitiesPerSector = maxEntitiesPerSector,
                AverageEntitiesPerSector = average
            };
        }
    }

    public async Task WarmupAroundSectorAsync(
        int mapId,
        int centerSectorX,
        int centerSectorY,
        int radius,
        CancellationToken cancellationToken
    )
    {
        if (!_spatialConfig.LazySectorItemLoadEnabled)
        {
            return;
        }

        for (var x = centerSectorX - radius; x <= centerSectorX + radius; x++)
        {
            for (var y = centerSectorY - radius; y <= centerSectorY + radius; y++)
            {
                await EnsureSectorLoadedAsync(mapId, x, y, cancellationToken);
            }
        }
    }

    public void RemoveEntity(Serial serial)
    {
        lock (_sync)
        {
            RemoveEntityUnsafe(serial);
        }
    }

    private void AddOrUpdateItemInternal(UOItemEntity item, int mapId, int sectorX, int sectorY)
    {
        lock (_sync)
        {
            RemoveEntityUnsafe(item.Id);
            item.MapId = mapId;
            var sector = GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
            sector.AddEntity(item);
            _entityLocations[item.Id] = new() { MapId = mapId, SectorX = sectorX, SectorY = sectorY };
        }
    }

    private void AddOrUpdateMobileInternal(UOMobileEntity mobile, int mapId, int sectorX, int sectorY)
    {
        lock (_sync)
        {
            RemoveEntityUnsafe(mobile.Id);
            mobile.MapId = mapId;
            var sector = GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
            sector.AddEntity(mobile);
            _entityLocations[mobile.Id] = new() { MapId = mapId, SectorX = sectorX, SectorY = sectorY };
        }
    }

    private bool MoveEntity(Serial serial, object entity, int mapId, Point3D oldLocation, Point3D newLocation)
    {
        var (oldX, oldY) = GetSectorCoordinates(oldLocation);
        var (newX, newY) = GetSectorCoordinates(newLocation);

        lock (_sync)
        {
            if (oldX == newX && oldY == newY)
            {
                _entityLocations[serial] = new() { MapId = mapId, SectorX = newX, SectorY = newY };

                return false;
            }

            if (_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                var oldSector = mapIndex.GetSector(oldX, oldY);
                var newSector = mapIndex.GetOrCreateSector(mapId, newX, newY);

                switch (entity)
                {
                    case UOMobileEntity mobile:
                        oldSector?.RemoveEntity(mobile);
                        newSector.AddEntity(mobile);

                        break;
                    case UOItemEntity item:
                        oldSector?.RemoveEntity(item);
                        newSector.AddEntity(item);

                        break;
                }
            }
            else
            {
                var sector = GetOrCreateSectorUnsafe(mapId, newX, newY);

                switch (entity)
                {
                    case UOMobileEntity mobile:
                        sector.AddEntity(mobile);

                        break;
                    case UOItemEntity item:
                        sector.AddEntity(item);

                        break;
                }
            }

            _entityLocations[serial] = new() { MapId = mapId, SectorX = newX, SectorY = newY };
        }

        return true;
    }

    private MapSector GetOrCreateSectorUnsafe(int mapId, int sectorX, int sectorY)
    {
        if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
        {
            mapIndex = new();
            _mapIndices[mapId] = mapIndex;
        }

        return mapIndex.GetOrCreateSector(mapId, sectorX, sectorY);
    }

    private void EnsureSectorLoaded(int mapId, int sectorX, int sectorY)
        => EnsureSectorLoadedAsync(mapId, sectorX, sectorY, CancellationToken.None).GetAwaiter().GetResult();

    private async Task EnsureSectorLoadedAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken)
    {
        if (!_spatialConfig.LazySectorItemLoadEnabled)
        {
            return;
        }

        var radius = Math.Max(0, _spatialConfig.LazySectorEntityLoadRadius);
        var loadTasks = new List<Task>();

        for (var x = sectorX - radius; x <= sectorX + radius; x++)
        {
            for (var y = sectorY - radius; y <= sectorY + radius; y++)
            {
                loadTasks.Add(EnsureSingleSectorLoadedAsync(mapId, x, y, cancellationToken));
            }
        }

        await Task.WhenAll(loadTasks).ConfigureAwait(false);
    }

    private async Task EnsureSingleSectorLoadedAsync(
        int mapId,
        int sectorX,
        int sectorY,
        CancellationToken cancellationToken
    )
    {
        var key = (mapId, sectorX, sectorY);
        Task loadTask;

        lock (_sync)
        {
            if (_loadedSectors.Contains(key))
            {
                return;
            }

            if (_sectorLoadTasks.TryGetValue(key, out loadTask!))
            {
                // Reuse in-flight load task.
            }
            else
            {
                loadTask = LoadSectorEntitiesAsync(mapId, sectorX, sectorY, cancellationToken);
                _sectorLoadTasks[key] = loadTask;
            }
        }

        try
        {
            await loadTask.ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                if (loadTask.IsCompletedSuccessfully)
                {
                    GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
                    _loadedSectors.Add(key);
                }

                if (loadTask.IsCompleted)
                {
                    _sectorLoadTasks.Remove(key);
                }
            }
        }
    }

    private async Task LoadSectorEntitiesAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mobiles = await _mobileService.GetPersistentMobilesInSectorAsync(
                          mapId,
                          sectorX,
                          sectorY,
                          cancellationToken
                      );

        foreach (var mobile in mobiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (mobile.IsPlayer)
            {
                continue;
            }

            var (mobileSectorX, mobileSectorY) = GetSectorCoordinates(mobile.Location);
            AddOrUpdateMobileInternal(mobile, mapId, mobileSectorX, mobileSectorY);
        }

        var items = await _itemService.GetGroundItemsInSectorAsync(mapId, sectorX, sectorY);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddOrUpdateItemInternal(item, mapId, sectorX, sectorY);
        }
    }

    private void RemoveEntityUnsafe(Serial serial)
    {
        if (!_entityLocations.TryGetValue(serial, out var location))
        {
            return;
        }

        if (_mapIndices.TryGetValue(location.MapId, out var mapIndex))
        {
            var sector = mapIndex.GetSector(location.SectorX, location.SectorY);

            if (sector is not null)
            {
                var mobile = sector.GetEntity<UOMobileEntity>(serial);
                var item = sector.GetEntity<UOItemEntity>(serial);

                if (mobile is not null)
                {
                    sector.RemoveEntity(mobile);
                }

                if (item is not null)
                {
                    sector.RemoveEntity(item);
                }
            }
        }

        _entityLocations.Remove(serial);
    }

    private static (int X, int Y) GetSectorCoordinates(Point3D location)
        => (location.X >> MapSectorConsts.SectorShift, location.Y >> MapSectorConsts.SectorShift);

    private static List<(int X, int Y)> GetSectorsInRange(Point3D location, int range)
    {
        var sectors = new List<(int X, int Y)>();
        var minX = (location.X - range) >> MapSectorConsts.SectorShift;
        var maxX = (location.X + range) >> MapSectorConsts.SectorShift;
        var minY = (location.Y - range) >> MapSectorConsts.SectorShift;
        var maxY = (location.Y + range) >> MapSectorConsts.SectorShift;

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                sectors.Add((x, y));
            }
        }

        return sectors;
    }

    private static List<(int X, int Y)> GetSectorCoordinatesInRadius(int centerSectorX, int centerSectorY, int radius)
    {
        var sectors = new List<(int X, int Y)>();

        for (var x = centerSectorX - radius; x <= centerSectorX + radius; x++)
        {
            for (var y = centerSectorY - radius; y <= centerSectorY + radius; y++)
            {
                sectors.Add((x, y));
            }
        }

        return sectors;
    }
}
