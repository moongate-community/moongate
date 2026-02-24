using Moongate.Server.Data.Session;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Spatial;

/// <summary>
/// Provides spatial indexing and range-query operations for world entities.
/// </summary>
public interface ISpatialWorldService
{
    /// <summary>
    /// Adds or updates a mobile position in the spatial index.
    /// </summary>
    /// <param name="mobile">Mobile entity.</param>
    void AddOrUpdateMobile(UOMobileEntity mobile);

    /// <summary>
    /// Adds or updates an item position in the spatial index.
    /// </summary>
    /// <param name="item">Item entity.</param>
    /// <param name="mapId">Owning map id.</param>
    void AddOrUpdateItem(UOItemEntity item, int mapId);

    /// <summary>
    /// Adds a region definition used by music lookup.
    /// </summary>
    /// <param name="region">Region definition.</param>
    void AddRegion(JsonRegion region);

    /// <summary>
    /// Adds music-list definitions.
    /// </summary>
    /// <param name="musics">Music definitions.</param>
    void AddMusics(List<JsonMusic> musics);

    /// <summary>
    /// Returns nearby items around a world location.
    /// </summary>
    /// <param name="location">Center location.</param>
    /// <param name="range">Tile range.</param>
    /// <param name="mapId">Map id.</param>
    /// <returns>Matching items.</returns>
    List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId);

    /// <summary>
    /// Returns nearby mobiles around a world location.
    /// </summary>
    /// <param name="location">Center location.</param>
    /// <param name="range">Tile range.</param>
    /// <param name="mapId">Map id.</param>
    /// <returns>Matching mobiles.</returns>
    List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId);

    /// <summary>
    /// Returns player sessions in range for packet broadcast.
    /// </summary>
    /// <param name="location">Center location.</param>
    /// <param name="range">Tile range.</param>
    /// <param name="mapId">Map id.</param>
    /// <param name="excludeSession">Optional excluded session.</param>
    /// <returns>Matching sessions.</returns>
    List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null);

    /// <summary>
    /// Resolves music id for a world location.
    /// </summary>
    /// <param name="location">Target location.</param>
    /// <returns>Music id, or <c>0</c> when no music mapping exists.</returns>
    int GetMusic(Point3D location);

    /// <summary>
    /// Moves a mobile in the spatial index.
    /// </summary>
    /// <param name="mobile">Mobile entity.</param>
    /// <param name="oldLocation">Previous location.</param>
    /// <param name="newLocation">New location.</param>
    void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation);

    /// <summary>
    /// Moves an item in the spatial index.
    /// </summary>
    /// <param name="item">Item entity.</param>
    /// <param name="mapId">Map id.</param>
    /// <param name="oldLocation">Previous location.</param>
    /// <param name="newLocation">New location.</param>
    void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation);

    /// <summary>
    /// Removes an entity from the index.
    /// </summary>
    /// <param name="serial">Entity serial.</param>
    void RemoveEntity(Serial serial);

    /// <summary>
    /// Returns current spatial index statistics.
    /// </summary>
    /// <returns>Sector stats.</returns>
    SectorSystemStats GetStats();
}
