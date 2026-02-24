using Moongate.Server.Data.Events;
using Moongate.Server.Data.Internal.Spatial;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Spatial;

/// <summary>
/// Default in-memory spatial world index based on map sectors.
/// </summary>
public sealed class SpatialWorldService
    : ISpatialWorldService, ISpatialMetricsSource, IGameEventListener<MobilePositionChangedEvent>, IGameEventListener<PlayerCharacterLoggedInEvent>
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, SpatialMapIndex> _mapIndices = [];
    private readonly Dictionary<Serial, SpatialEntityLocation> _entityLocations = [];
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly List<JsonRegion> _regions = [];
    private readonly Dictionary<int, int> _musicByListId = [];

    private readonly ICharacterService _characterService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpatialWorldService"/> class.
    /// </summary>
    /// <param name="gameNetworkSessionService">Session lookup service.</param>
    /// <param name="gameEventBusService">Game event bus service.</param>
    public SpatialWorldService(
        IGameNetworkSessionService gameNetworkSessionService,
        IGameEventBusService gameEventBusService,
        ICharacterService characterService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _gameEventBusService = gameEventBusService;
        _characterService = characterService;

        _gameEventBusService.RegisterListener<MobilePositionChangedEvent>(this);
        _gameEventBusService.RegisterListener<PlayerCharacterLoggedInEvent>(this);
    }

    public void AddOrUpdateMobile(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        var mapId = mobile.MapId;
        var (sectorX, sectorY) = GetSectorCoordinates(mobile.Location);
        MobileAddedInSectorEvent? gameEvent = null;

        lock (_sync)
        {
            var isNew = !_entityLocations.ContainsKey(mobile.Id);
            RemoveEntityUnsafe(mobile.Id);
            var sector = GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
            sector.AddEntity(mobile);
            _entityLocations[mobile.Id] = new SpatialEntityLocation { MapId = mapId, SectorX = sectorX, SectorY = sectorY };

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

    public void AddOrUpdateItem(UOItemEntity item, int mapId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (sectorX, sectorY) = GetSectorCoordinates(item.Location);

        lock (_sync)
        {
            RemoveEntityUnsafe(item.Id);
            var sector = GetOrCreateSectorUnsafe(mapId, sectorX, sectorY);
            sector.AddEntity(item);
            _entityLocations[item.Id] = new SpatialEntityLocation { MapId = mapId, SectorX = sectorX, SectorY = sectorY };
        }
    }

    public void AddRegion(JsonRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        lock (_sync)
        {
            _regions.Add(region);
        }
    }

    public void AddMusics(List<JsonMusic> musics)
    {
        ArgumentNullException.ThrowIfNull(musics);

        lock (_sync)
        {
            foreach (var music in musics)
            {
                _musicByListId[music.Id] = music.Music;
            }
        }
    }

    public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
    {
        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return [];
            }

            var sectors = GetSectorsInRange(location, range);
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
        lock (_sync)
        {
            if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
            {
                return [];
            }

            var sectors = GetSectorsInRange(location, range);
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

    public int GetMusic(Point3D location)
    {
        lock (_sync)
        {
            foreach (var region in _regions)
            {
                if (region.Coordinates.Any(coordinate => coordinate.Contains(location.X, location.Y)) &&
                    _musicByListId.TryGetValue(region.MusicList, out var music))
                {
                    return music;
                }
            }
        }

        return 0;
    }

    public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        mobile.Location = newLocation;
        var mapId = mobile.MapId;
        var sectorChanged = MoveEntity(mobile.Id, mobile, mapId, oldLocation, newLocation);

        if (sectorChanged)
        {
            var (oldX, oldY) = GetSectorCoordinates(oldLocation);
            var (newX, newY) = GetSectorCoordinates(newLocation);
            PublishEvent(new MobileSectorChangedEvent(mobile.Id, mapId, oldX, oldY, newX, newY));
        }
    }

    public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Location = newLocation;
        _ = MoveEntity(item.Id, item, mapId, oldLocation, newLocation);
    }

    public void RemoveEntity(Serial serial)
    {
        lock (_sync)
        {
            RemoveEntityUnsafe(serial);
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

            return new SectorSystemStats
            {
                TotalSectors = totalSectors,
                TotalEntities = totalEntities,
                MaxEntitiesPerSector = maxEntitiesPerSector,
                AverageEntitiesPerSector = average
            };
        }
    }

    public SectorSystemStats GetMetricsSnapshot()
        => GetStats();

    private bool MoveEntity(Serial serial, object entity, int mapId, Point3D oldLocation, Point3D newLocation)
    {
        var (oldX, oldY) = GetSectorCoordinates(oldLocation);
        var (newX, newY) = GetSectorCoordinates(newLocation);

        lock (_sync)
        {
            if (oldX == newX && oldY == newY)
            {
                _entityLocations[serial] = new SpatialEntityLocation { MapId = mapId, SectorX = newX, SectorY = newY };

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

            _entityLocations[serial] = new SpatialEntityLocation { MapId = mapId, SectorX = newX, SectorY = newY };
        }

        return true;
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

    private MapSector GetOrCreateSectorUnsafe(int mapId, int sectorX, int sectorY)
    {
        if (!_mapIndices.TryGetValue(mapId, out var mapIndex))
        {
            mapIndex = new SpatialMapIndex();
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

    private void PublishEvent<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        => _gameEventBusService.PublishAsync(gameEvent).AsTask().GetAwaiter().GetResult();

    public async Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) &&
            session.CharacterId == gameEvent.MobileId)
        {
            OnMobileMoved(session.Character!, gameEvent.OldLocation, gameEvent.NewLocation);
        }
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var character = await _characterService.GetCharacterAsync(gameEvent.CharacterId);

        AddOrUpdateMobile(character);

    }
}
