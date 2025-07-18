using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Utils;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services;

/// <summary>
/// High-level service that integrates the spatial indexing system with your existing services
/// Provides easy-to-use methods for common spatial queries
/// </summary>
public class SpatialWorldService : ISpatialWorldService
{
    private readonly ILogger _logger = Log.ForContext<SpatialWorldService>();
    private readonly MapSectorSystem _sectorSystem;
    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IGameSessionService _gameSessionService;

    private readonly Dictionary<Rectangle2D, JsonRegion> _regionsDefinition = new();

    private readonly Dictionary<int, JsonMusic> _musicDefinition = new();


    public event ISpatialWorldService.EntityMovedSectorHandler? EntityMovedSector;
    public event ISpatialWorldService.MobileSectorMovedHandler? MobileSectorMoved;
    public event ISpatialWorldService.MobileInSectorHandler? OnMobileAddedInSector;
    public event ISpatialWorldService.MobileExitSectorHandler? OnMobileExitSector;
    public event ISpatialWorldService.ItemMovedOnGroundHandler? ItemMovedOnGround;
    public event ISpatialWorldService.ItemMovedOnContainerHandler? ItemMovedOnContainer;
    public event ISpatialWorldService.ItemPickedUpHandler? ItemPickedUp;
    public event ISpatialWorldService.ItemRemovedHandler? ItemRemoved;
    public event ISpatialWorldService.MobileMovedHandler? MobileMoved;

    public SpatialWorldService(
        IItemService itemService, IMobileService mobileService, IDiagnosticService diagnosticService,
        IGameSessionService gameSessionService
    )
    {
        _itemService = itemService;
        _mobileService = mobileService;
        _diagnosticService = diagnosticService;
        _gameSessionService = gameSessionService;
        _sectorSystem = new MapSectorSystem();

        _sectorSystem.EntityMovedSector += (entity, oldSector, newSector) =>
        {
            if (entity is UOMobileEntity mobile)
            {
                MobileSectorMoved?.Invoke(mobile, oldSector, newSector);
                var worldView = GetPlayerWorldView(mobile);
                OnMobileAddedInSector?.Invoke(mobile, newSector, worldView);

                if (oldSector != newSector)
                {
                    OnMobileExitSector?.Invoke(mobile, oldSector, worldView);
                }
            }

            EntityMovedSector?.Invoke(entity, oldSector, newSector);
        };

        /// Subscribe to entity events to keep spatial index updated
        SubscribeToEntityEvents();
    }

    private void SubscribeToEntityEvents()
    {
        /// When items are created/added, add them to spatial index
        _itemService.ItemCreated += OnItemCreated;
        _itemService.ItemAdded += OnItemAdded;

        /// When mobiles are created/added, add them to spatial index
        _mobileService.MobileCreated += OnMobileCreated;
        _mobileService.MobileAdded += OnMobileAdded;

        _mobileService.MobileMoved += OnMobileMoved;
        _itemService.ItemMoved += OnItemMoved;
    }

    #region Entity Event Handlers

    private void OnItemCreated(UOItemEntity item)
    {
        if (item.Location != Point3D.Zero && !item.IsOnGround)
        {
            /// Only add items that are on the ground to spatial index
            return;
        }

        var mapIndex = GetMapIndex(item);
        _sectorSystem.AddEntity(item, mapIndex);

        _logger.Verbose("Added item {Serial} to spatial index at {Location}", item.Id, item.Location);
    }

    private void OnItemAdded(UOItemEntity item)
    {
        OnItemCreated(item); /// Same logic
    }

    private void OnMobileCreated(UOMobileEntity mobile)
    {
        var mapIndex = GetMapIndex(mobile);
        _sectorSystem.AddEntity(mobile, mapIndex);

        _logger.Verbose("Added mobile {Serial} to spatial index at {Location}", mobile.Id, mobile.Location);

        var sector = _sectorSystem.GetSectorByWorldCoordinates(mobile.Map.MapID, mobile.Location.X, mobile.Location.Y);
        var worldView = GetPlayerWorldView(mobile);

        OnMobileAddedInSector?.Invoke(mobile, sector, worldView);

    }

    private void OnMobileAdded(UOMobileEntity mobile)
    {
        OnMobileCreated(mobile);
    }


    /// <summary>
    /// Call this when a mobile moves
    /// </summary>
    public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
    {
        var mapIndex = GetMapIndex(mobile);
        _sectorSystem.MoveEntity(mobile, mapIndex, oldLocation, newLocation);

        var worldView = GetPlayerWorldView(mobile);

        MobileMoved?.Invoke(mobile, newLocation, worldView);


        _logger.Debug(
            "Moved mobile {Serial} from {OldLocation} to {NewLocation}",
            mobile.Id,
            oldLocation,
            newLocation
        );
    }

    /// <summary>
    /// Call this when an item moves (dropped, picked up, etc.)
    /// </summary>
    public void OnItemMoved(UOItemEntity item, Point3D oldLocation, Point3D newLocation, bool isOnGround)
    {
        // check if was on ground

        if (item.IsOnGround && !isOnGround)
        {
            /// Item was picked up from ground, remove from spatial index

            _sectorSystem.RemoveEntity(item, GetMapIndex(item));

            _logger.Verbose(
                "Removed item {Serial} from spatial index at {Location}",
                item.Id,
                oldLocation
            );

            ItemRemoved?.Invoke(
                item,
                oldLocation,
                newLocation,
                GetNearbyMobiles(newLocation, MapSectorConsts.MaxViewRange, item.Map.MapID)
            );
        }

        // Not not ground
        if (!isOnGround)
        {
            UOMobileEntity mobile = null;

            if (item.OwnerId != Serial.Zero)
            {
                mobile = _mobileService.GetMobile(item.OwnerId);
            }

            ItemMovedOnContainer?.Invoke(item, oldLocation, newLocation, GetPlayerWorldView(mobile));

            return;
        }

        var mapIndex = GetMapIndex(item);

        _sectorSystem.MoveEntity(item, mapIndex, oldLocation, newLocation);

        _logger.Verbose(
            "Moved item {Serial} from {OldLocation} to {NewLocation}",
            item.Id,
            oldLocation,
            newLocation
        );

        ItemMovedOnGround?.Invoke(
            item,
            oldLocation,
            newLocation,
            GetNearbyMobiles(newLocation, MapSectorConsts.MaxViewRange, mapIndex)
        );
    }

    #endregion

    #region Spatial Queries

    /// <summary>
    /// Gets all players within view range of a location (for packet broadcasting)
    /// </summary>
    public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapIndex, GameSession? excludeSession = null)
    {
        var players = _sectorSystem.GetPlayersInRange(location, range, mapIndex);

        return players
            .Select(player =>
                _gameSessionService.QuerySessionFirstOrDefault(s => s.Mobile != null && s.Mobile.Id == player.Id)
            )
            .OfType<GameSession>()
            .Where(session => session != excludeSession)
            .ToList();
    }

    /// <summary>
    /// Gets all items near a location (for ground item display)
    /// </summary>
    public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapIndex)
    {
        return _sectorSystem.GetItemsInRange(location, range, mapIndex)
            .Where(item => item.IsOnGround) /// Only ground items
            .ToList();
    }

    /// <summary>
    /// Gets all mobiles near a location (for client display)
    /// </summary>
    public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapIndex)
    {
        return _sectorSystem.GetMobilesInViewRange(location, mapIndex, range);
    }

    /// <summary>
    /// Fast lookup for any entity by serial
    /// </summary>
    public T? FindEntity<T>(Serial serial) where T : class, IPositionEntity
    {
        /// Try spatial index first (fastest)
        var entity = _sectorSystem.FindEntity<T>(serial);
        if (entity != null)
            return entity;

        /// Fallback to service lookups if not in spatial index
        if (typeof(T) == typeof(UOItemEntity))
        {
            return _itemService.GetItem(serial) as T;
        }

        if (typeof(T) == typeof(UOMobileEntity))
        {
            return _mobileService.GetMobile(serial) as T;
        }

        return default;
    }

    /// <summary>
    /// Gets all entities visible to a player (for initial login)
    /// </summary>
    public WorldView GetPlayerWorldView(UOMobileEntity player, int viewRange = 24)
    {
        var mapIndex = GetMapIndex(player);

        var nearbyMobiles = GetNearbyMobiles(player.Location, viewRange, mapIndex)
            .Where(m => m.Id != player.Id) /// Exclude self
            .ToList();

        var nearbyItems = GetNearbyItems(player.Location, viewRange, mapIndex);

        return new WorldView
        {
            Player = player,
            NearbyMobiles = nearbyMobiles,
            NearbyItems = nearbyItems,
            ViewRange = viewRange,
            MapIndex = mapIndex
        };
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets map index for an entity
    /// </summary>
    private static int GetMapIndex(IPositionEntity entity)
    {
        return entity.Map.Index;
    }

    /// <summary>
    /// Removes an entity from spatial index (call when entity is deleted)
    /// </summary>
    public void RemoveEntity(IPositionEntity entity, int mapIndex)
    {
        _sectorSystem.RemoveEntity(entity, mapIndex);
    }

    /// <summary>
    /// Gets statistics about the spatial system
    /// </summary>
    public SectorSystemStats GetStats()
    {
        return _sectorSystem.GetStats();
    }

    public void AddRegion(JsonRegion region)
    {
        foreach (var regionCoordinate in region.Coordinates)
        {
            var rectangle = new Rectangle2D(
                regionCoordinate.X1,
                regionCoordinate.Y1,
                regionCoordinate.Width,
                regionCoordinate.Height
            );

            if (!_regionsDefinition.TryAdd(rectangle, region))
            {
                _logger.Warning("Region {RegionId} already exists in the spatial index", region.Id);
                continue;
            }

            _logger.Information("Added region {RegionId} {RegionName} to spatial index", region.Id, region.Name);
        }
    }

    public void AddMusics(List<JsonMusic> musics)
    {
        foreach (var music in musics)
        {
            if (!_musicDefinition.TryAdd(music.Id, music))
            {
                continue;
            }
        }
    }

    public int GetMusicFromLocation(Point3D location, int mapIndex)
    {
        /// Find the region that contains this location
        foreach (var region in _regionsDefinition)
        {
            if (region.Key.Contains(location.X, location.Y))
            {
                return _musicDefinition[region.Value.MusicList].Music;
            }
        }

        return 0;
    }

    public JsonRegion? GetRegionFromLocation(Point3D location, int mapIndex)
    {
        foreach (var region in _regionsDefinition)
        {
            if (region.Key.Contains(location.X, location.Y))
            {
                return region.Value;
            }
        }

        return null; /// No region found
    }

    #endregion

    public void Dispose()
    {
        /// Unsubscribe from events
        _itemService.ItemCreated -= OnItemCreated;
        _itemService.ItemAdded -= OnItemAdded;
        _mobileService.MobileCreated -= OnMobileCreated;
        _mobileService.MobileAdded -= OnMobileAdded;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _diagnosticService.RegisterMetricsProvider(this);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string ProviderName => "SpatialWorldService";

    public object GetMetrics()
    {
        return _sectorSystem.GetStats();
    }
}
