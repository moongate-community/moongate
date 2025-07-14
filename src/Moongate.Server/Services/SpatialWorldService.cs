using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;
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


    public event ISpatialWorldService.EntityMovedSectorHandler? EntityMovedSector;
    public event ISpatialWorldService.MobileSectorMovedHandler? MobileSectorMoved;
    public event ISpatialWorldService.MobileMovedHandler? MobileMoved;

    public SpatialWorldService(IItemService itemService, IMobileService mobileService, IDiagnosticService diagnosticService)
    {
        _itemService = itemService;
        _mobileService = mobileService;
        _diagnosticService = diagnosticService;
        _sectorSystem = new MapSectorSystem();

        _sectorSystem.EntityMovedSector += (entity, sector, newSector) =>
        {
            if (entity is UOMobileEntity mobile)
            {
                MobileSectorMoved?.Invoke(mobile, sector, newSector);
            }

            EntityMovedSector?.Invoke(entity, sector, newSector);
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
    }

    private void OnMobileAdded(UOMobileEntity mobile)
    {
        OnMobileCreated(mobile); /// Same logic
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


        _logger.Verbose(
            "Moved mobile {Serial} from {OldLocation} to {NewLocation}",
            mobile.Id,
            oldLocation,
            newLocation
        );
    }

    /// <summary>
    /// Call this when an item moves (dropped, picked up, etc.)
    /// </summary>
    public void OnItemMoved(UOItemEntity item, Point3D oldLocation, Point3D newLocation)
    {
        var mapIndex = GetMapIndex(item);
        _sectorSystem.MoveEntity(item, mapIndex, oldLocation, newLocation);

        _logger.Verbose(
            "Moved item {Serial} from {OldLocation} to {NewLocation}",
            item.Id,
            oldLocation,
            newLocation
        );
    }

    #endregion

    #region Spatial Queries

    /// <summary>
    /// Gets all players within view range of a location (for packet broadcasting)
    /// </summary>
    public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapIndex, GameSession? excludeSession = null)
    {
        var players = _sectorSystem.GetPlayersInRange(location, mapIndex, range);
        var sessions = new List<GameSession>();

        foreach (var player in players)
        {
            /// TODO: Get session for player - you'll need to implement this based on your session management
            /// var session = GetSessionForPlayer(player);
            /// if (session != null && session != excludeSession)
            /// {
            ///     sessions.Add(session);
            /// }
        }

        return sessions;
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
    private int GetMapIndex(IPositionEntity entity)
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
