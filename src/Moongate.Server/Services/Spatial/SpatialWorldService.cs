using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Spatial;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.Spatial;

/// <summary>
/// Default in-memory spatial world index based on map sectors.
/// </summary>
[RegisterGameEventListener]
public sealed class SpatialWorldService
    : ISpatialWorldService, ISpatialMetricsSource,
      IGameEventListener<MobilePositionChangedEvent>,
      IGameEventListener<PlayerCharacterLoggedInEvent>,
      IGameEventListener<DropItemToGroundEvent>
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, SpatialMapIndex> _mapIndices = [];
    private readonly Dictionary<Serial, SpatialEntityLocation> _entityLocations = [];
    private readonly Dictionary<(int MapId, int SectorX, int SectorY), List<JsonRegion>> _regionsBySector = [];
    private readonly Dictionary<JsonRegion, int> _regionChildLevels = [];
    private bool _regionIndexDirty = true;
    private readonly HashSet<(int MapId, int SectorX, int SectorY)> _loadedSectors = [];
    private readonly Dictionary<(int MapId, int SectorX, int SectorY), Task> _sectorLoadTasks = [];
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly List<JsonRegion> _regions = [];

    private readonly ICharacterService _characterService;
    private readonly IItemService _itemService;
    private readonly MoongateSpatialConfig _spatialConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpatialWorldService" /> class.
    /// </summary>
    /// <param name="gameNetworkSessionService">Session lookup service.</param>
    /// <param name="gameEventBusService">Game event bus service.</param>
    public SpatialWorldService(
        IGameNetworkSessionService gameNetworkSessionService,
        IGameEventBusService gameEventBusService,
        ICharacterService characterService,
        IItemService itemService,
        IOutgoingPacketQueue outgoingPacketQueue,
        MoongateConfig moongateConfig
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _gameEventBusService = gameEventBusService;
        _characterService = characterService;
        _itemService = itemService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _spatialConfig = moongateConfig.Spatial ?? new();

    }

    public void AddOrUpdateItem(UOItemEntity item, int mapId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (sectorX, sectorY) = GetSectorCoordinates(item.Location);
        EnsureSectorLoaded(mapId, sectorX, sectorY);
        AddOrUpdateItemInternal(item, mapId, sectorX, sectorY);
    }

    public void AddOrUpdateMobile(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        var mapId = mobile.MapId;
        var (sectorX, sectorY) = GetSectorCoordinates(mobile.Location);
        EnsureSectorLoaded(mapId, sectorX, sectorY);
        MobileAddedInSectorEvent? gameEvent = null;

        lock (_sync)
        {
            var isNew = !_entityLocations.ContainsKey(mobile.Id);
            RemoveEntityUnsafe(mobile.Id);
            var sector = GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
            sector.AddEntity(mobile);
            _entityLocations[mobile.Id] = new() { MapId = mapId, SectorX = sectorX, SectorY = sectorY };

            if (isNew)
            {
                gameEvent = new MobileAddedInSectorEvent(mobile.Id, mapId, sectorX, sectorY);
            }
        }

        if (gameEvent.HasValue)
        {
            PublishEvent(gameEvent.Value);
        }
    }

    public void AddRegion(JsonRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        lock (_sync)
        {
            _regions.Add(region);
            _regionIndexDirty = true;
        }
    }

    public SectorSystemStats GetMetricsSnapshot()
        => GetStats();

    public int GetMusic(int mapId, Point3D location)
    {
        lock (_sync)
        {
            var regions = GetCandidateRegionsUnsafe(mapId, location);

            foreach (var region in regions)
            {
                if (region.MapId != mapId)
                {
                    continue;
                }

                if (!region.Area.Any(coordinate => coordinate.Contains(location.X, location.Y)))
                {
                    continue;
                }

                if (region.Music.HasValue)
                {
                    return (int)region.Music.Value;
                }
            }
        }

        return 0;
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

    public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
    {
        var players = GetNearbyMobiles(location, range, mapId).Where(static mobile => mobile.IsPlayer).ToList();
        var sessions = _gameNetworkSessionService.GetAll();
        var sessionsByCharacter = sessions
                                  .Where(static session => session.Character is not null)
                                  .ToDictionary(static session => session.Character!.Id, static session => session);
        var result = new List<GameSession>();

        foreach (var player in players)
        {
            if (sessionsByCharacter.TryGetValue(player.Id, out var session) &&
                session != excludeSession)
            {
                result.Add(session);
            }
        }

        return result;
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

    public List<MapSector> GetActiveSectors()
    {
        lock (_sync)
        {
            return [.. _mapIndices.Values.SelectMany(static mapIndex => mapIndex.Sectors)];
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

    public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) &&
                session.CharacterId == gameEvent.MobileId)
            {
                OnMobileMoved(session.Character!, gameEvent.OldLocation, gameEvent.NewLocation);
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var character = await _characterService.GetCharacterAsync(gameEvent.CharacterId);
        var (sectorX, sectorY) = GetSectorCoordinates(character.Location);
        var warmupRadius = Math.Max(0, _spatialConfig.SectorWarmupRadius);

        await WarmupAroundSectorAsync(character.MapId, sectorX, sectorY, warmupRadius, cancellationToken);

        AddOrUpdateMobile(character);

        var region = ResolveRegion(character.MapId, character.Location);

        if (region is not null)
        {
            PublishEvent(new PlayerEnteredRegionEvent(character.Id, character.MapId, region.Id, region.Name));
        }
    }

    public async Task HandleAsync(DropItemToGroundEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var item = await _itemService.GetItemAsync(gameEvent.ItemId);

        if (item is null)
        {
            return;
        }

        var mapId = 0;

        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var runtimeSession) &&
            runtimeSession.Character is not null &&
            runtimeSession.CharacterId == gameEvent.MobileId)
        {
            mapId = runtimeSession.Character.MapId;
        }
        else
        {
            var character = await _characterService.GetCharacterAsync(gameEvent.MobileId);

            if (character is null)
            {
                return;
            }

            mapId = character.MapId;
        }

        var sector = GetSectorByLocation(mapId, gameEvent.NewLocation);

        if (sector is null)
        {
            return;
        }

        var players = GetPlayersInSector(mapId, sector.SectorX, sector.SectorY);

        var dropPacket = new ObjectInformationPacket(item);

        foreach (var player in players)
        {
            if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
            {
                _outgoingPacketQueue.Enqueue(session.SessionId, dropPacket);
            }
        }
    }

    public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Location = newLocation;
        _ = MoveEntity(item.Id, item, mapId, oldLocation, newLocation);
    }

    public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        mobile.Location = newLocation;
        var mapId = mobile.MapId;
        var oldRegion = ResolveRegion(mapId, oldLocation);
        var newRegion = ResolveRegion(mapId, newLocation);
        var sectorChanged = MoveEntity(mobile.Id, mobile, mapId, oldLocation, newLocation);

        if (sectorChanged)
        {
            var (oldX, oldY) = GetSectorCoordinates(oldLocation);
            var (newX, newY) = GetSectorCoordinates(newLocation);
            PublishEvent(new MobileSectorChangedEvent(mobile.Id, mapId, oldX, oldY, newX, newY));
        }

        if (!mobile.IsPlayer)
        {
            return;
        }

        var oldRegionId = oldRegion?.Id;
        var newRegionId = newRegion?.Id;

        if (oldRegionId == newRegionId)
        {
            return;
        }

        if (oldRegion is not null)
        {
            PublishEvent(new PlayerExitedRegionEvent(mobile.Id, mapId, oldRegion.Id, oldRegion.Name));
        }

        if (newRegion is not null)
        {
            PublishEvent(new PlayerEnteredRegionEvent(mobile.Id, mapId, newRegion.Id, newRegion.Name));
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

    private void EnsureSectorLoaded(int mapId, int sectorX, int sectorY)
        => EnsureSectorLoadedAsync(mapId, sectorX, sectorY, CancellationToken.None).GetAwaiter().GetResult();

    private async Task EnsureSectorLoadedAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken)
    {
        if (!_spatialConfig.LazySectorItemLoadEnabled)
        {
            return;
        }

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
                loadTask = LoadSectorItemsAsync(mapId, sectorX, sectorY, cancellationToken);
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
                    _loadedSectors.Add(key);
                }

                if (loadTask.IsCompleted)
                {
                    _sectorLoadTasks.Remove(key);
                }
            }
        }
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

    private async Task LoadSectorItemsAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = await _itemService.GetGroundItemsInSectorAsync(mapId, sectorX, sectorY);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddOrUpdateItemInternal(item, mapId, sectorX, sectorY);
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

    private JsonRegion? ResolveRegion(int mapId, Point3D location)
    {
        lock (_sync)
        {
            var regions = GetCandidateRegionsUnsafe(mapId, location);

            return regions.FirstOrDefault(
                region => region.MapId == mapId &&
                          region.Area.Any(coordinate => coordinate.Contains(location.X, location.Y))
            );
        }
    }

    private List<JsonRegion> GetCandidateRegionsUnsafe(int mapId, Point3D location)
    {
        EnsureRegionIndexUnsafe();
        var (sectorX, sectorY) = GetSectorCoordinates(location);

        if (_regionsBySector.TryGetValue((mapId, sectorX, sectorY), out var bySector))
        {
            return bySector;
        }

        return _regions;
    }

    private void EnsureRegionIndexUnsafe()
    {
        if (!_regionIndexDirty)
        {
            return;
        }

        _regionsBySector.Clear();
        _regionChildLevels.Clear();

        var byName = _regions
                     .Where(static region => !string.IsNullOrWhiteSpace(region.Name))
                     .GroupBy(static region => (region.MapId, region.Name), static region => region)
                     .ToDictionary(static group => group.Key, static group => group.First());

        foreach (var region in _regions)
        {
            _ = ComputeChildLevelUnsafe(region, byName, []);
        }

        foreach (var region in _regions)
        {
            foreach (var coordinate in region.Area)
            {
                var minX = Math.Min(coordinate.X1, coordinate.X2) >> MapSectorConsts.SectorShift;
                var maxX = Math.Max(coordinate.X1, coordinate.X2) >> MapSectorConsts.SectorShift;
                var minY = Math.Min(coordinate.Y1, coordinate.Y2) >> MapSectorConsts.SectorShift;
                var maxY = Math.Max(coordinate.Y1, coordinate.Y2) >> MapSectorConsts.SectorShift;

                for (var sectorX = minX; sectorX <= maxX; sectorX++)
                {
                    for (var sectorY = minY; sectorY <= maxY; sectorY++)
                    {
                        var key = (region.MapId, sectorX, sectorY);

                        if (!_regionsBySector.TryGetValue(key, out var list))
                        {
                            list = [];
                            _regionsBySector[key] = list;
                        }

                        if (!list.Contains(region))
                        {
                            list.Add(region);
                        }
                    }
                }
            }
        }

        foreach (var list in _regionsBySector.Values)
        {
            list.Sort(CompareRegionOrderUnsafe);
        }

        _regionIndexDirty = false;
    }

    private int ComputeChildLevelUnsafe(
        JsonRegion region,
        IReadOnlyDictionary<(int MapId, string Name), JsonRegion> byName,
        HashSet<JsonRegion> visiting
    )
    {
        if (_regionChildLevels.TryGetValue(region, out var cached))
        {
            return cached;
        }

        if (!visiting.Add(region))
        {
            return 0;
        }

        var level = 0;

        if (region is JsonTownRegion town && town.Parent is not null &&
            byName.TryGetValue((town.Parent.MapId, town.Parent.Name), out var parent))
        {
            level = ComputeChildLevelUnsafe(parent, byName, visiting) + 1;
        }

        visiting.Remove(region);
        _regionChildLevels[region] = level;

        return level;
    }

    private int CompareRegionOrderUnsafe(JsonRegion left, JsonRegion right)
    {
        var byPriority = right.Priority.CompareTo(left.Priority);

        if (byPriority != 0)
        {
            return byPriority;
        }

        var leftLevel = _regionChildLevels.TryGetValue(left, out var ll) ? ll : 0;
        var rightLevel = _regionChildLevels.TryGetValue(right, out var rl) ? rl : 0;

        return rightLevel.CompareTo(leftLevel);
    }

    private void PublishEvent<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        => _gameEventBusService.PublishAsync(gameEvent).AsTask().GetAwaiter().GetResult();

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

    private async Task WarmupAroundSectorAsync(
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
}
