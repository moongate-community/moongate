using Moongate.Core.Interfaces.Metrics;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Data.Interfaces.Services;

/// <summary>
/// Interface for spatial world service that provides spatial indexing and querying capabilities
/// </summary>
public interface ISpatialWorldService : IMoongateAutostartService, IMetricsProvider
{
    delegate void EntityMovedSectorHandler(IPositionEntity entity, MapSector oldSector, MapSector newSector);

    delegate void MobileSectorMovedHandler(UOMobileEntity mobile, MapSector oldSector, MapSector newSector);

    delegate void MobileMovedHandler(UOMobileEntity mobile, Point3D location, WorldView worldView);

    delegate void MobileInSectorHandler(UOMobileEntity mobile, MapSector sector, WorldView worldView);

    delegate void MobileExitSectorHandler(UOMobileEntity mobile, MapSector sector, WorldView worldView);

    delegate void ItemMovedOnGroundHandler(
        UOItemEntity item, Point3D oldLocation, Point3D newLocation, List<UOMobileEntity> mobiles
    );

    delegate void ItemMovedOnContainerHandler(
        UOItemEntity item, Point3D oldLocation, Point3D newLocation, WorldView worldView
    );

    delegate void ItemPickedUpHandler(
        UOItemEntity item, Point3D oldLocation, Point3D newLocation, WorldView worldView
    );

    delegate void ItemRemovedHandler(
        UOItemEntity item, Point3D oldLocation, Point3D newLocation, List<UOMobileEntity> mobiles
    );

    event EntityMovedSectorHandler EntityMovedSector;
    event MobileSectorMovedHandler MobileSectorMoved;
    event MobileInSectorHandler OnMobileAddedInSector;
    event MobileExitSectorHandler OnMobileExitSector;
    event ItemMovedOnGroundHandler ItemMovedOnGround;
    event ItemMovedOnContainerHandler ItemMovedOnContainer;
    event ItemPickedUpHandler ItemPickedUp;

    event ItemRemovedHandler ItemRemoved;



    event MobileMovedHandler MobileMoved;


    /// <summary>
    /// Call this when a mobile moves to update spatial index
    /// </summary>
    /// <param name="mobile">Mobile that moved</param>
    /// <param name="oldLocation">Previous location</param>
    /// <param name="newLocation">New location</param>
    void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation);

    /// <summary>
    /// Call this when an item moves (dropped, picked up, etc.) to update spatial index
    /// </summary>
    /// <param name="item">Item that moved</param>
    /// <param name="oldLocation">Previous location</param>
    /// <param name="newLocation">New location</param>
    void OnItemMoved(UOItemEntity item, Point3D oldLocation, Point3D newLocation, bool isOnGround);


    /// <summary>
    /// Gets all players within view range of a location (for packet broadcasting)
    /// </summary>
    /// <param name="location">Center location</param>
    /// <param name="range">Range in tiles</param>
    /// <param name="mapIndex">Map index</param>
    /// <param name="excludeSession">Session to exclude from results</param>
    /// <returns>List of game sessions within range</returns>
    List<GameSession> GetPlayersInRange(Point3D location, int range, int mapIndex, GameSession? excludeSession = null);

    /// <summary>
    /// Gets all items near a location (for ground item display)
    /// </summary>
    /// <param name="location">Center location</param>
    /// <param name="range">Range in tiles</param>
    /// <param name="mapIndex">Map index</param>
    /// <returns>List of items within range</returns>
    List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapIndex);

    /// <summary>
    /// Gets all mobiles near a location (for client display)
    /// </summary>
    /// <param name="location">Center location</param>
    /// <param name="range">Range in tiles</param>
    /// <param name="mapIndex">Map index</param>
    /// <returns>List of mobiles within range</returns>
    List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapIndex);

    /// <summary>
    /// Fast lookup for any entity by serial
    /// </summary>
    /// <typeparam name="T">Type of entity to find</typeparam>
    /// <param name="serial">Serial of the entity</param>
    /// <returns>Entity if found, null otherwise</returns>
    T? FindEntity<T>(Serial serial) where T : class, IPositionEntity;

    /// <summary>
    /// Gets all entities visible to a player (for initial login)
    /// </summary>
    /// <param name="player">Player to get world view for</param>
    /// <param name="viewRange">View range in tiles (default 24)</param>
    /// <returns>WorldView containing all visible entities</returns>
    WorldView GetPlayerWorldView(UOMobileEntity player, int viewRange = 24);


    /// <summary>
    /// Removes an entity from spatial index (call when entity is deleted)
    /// </summary>
    /// <param name="entity">Entity to remove</param>
    /// <param name="mapIndex">Map index</param>
    void RemoveEntity(IPositionEntity entity, int mapIndex);


    /// <summary>
    /// Gets statistics about the spatial system performance
    /// </summary>
    /// <returns>Statistics about sectors and entities</returns>
    SectorSystemStats GetStats();
}
