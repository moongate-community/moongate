using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Interfaces.Services.Spatial;

/// <summary>
/// Provides spatial indexing and range-query operations for world entities.
/// </summary>
public interface ISpatialWorldService
{
    /// <summary>
    /// Adds or updates an item position in the spatial index.
    /// </summary>
    /// <param name="item">Item entity.</param>
    /// <param name="mapId">Owning map id.</param>
    void AddOrUpdateItem(UOItemEntity item, int mapId);

    /// <summary>
    /// Adds or updates a mobile position in the spatial index.
    /// </summary>
    /// <param name="mobile">Mobile entity.</param>
    void AddOrUpdateMobile(UOMobileEntity mobile);

    /// <summary>
    /// Adds a region definition used for spatial region lookup and music mapping.
    /// </summary>
    /// <param name="region">Region definition.</param>
    void AddRegion(JsonRegion region);

    /// <summary>
    /// Broadcasts a packet to player sessions within range on a map.
    /// </summary>
    /// <param name="packet">Packet to enqueue.</param>
    /// <param name="mapId">Map id.</param>
    /// <param name="location">Center location.</param>
    /// <param name="range">
    /// Optional tile range. When <see langword="null" />, the service uses
    /// <c>MoongateSpatialConfig.SectorEnterSyncRadius</c>.
    /// </param>
    /// <param name="excludeSessionId">Optional session id to exclude.</param>
    /// <returns>Number of recipient sessions.</returns>
    Task<int> BroadcastToPlayersAsync(
        IGameNetworkPacket packet,
        int mapId,
        Point3D location,
        int? range = null,
        long? excludeSessionId = null
    );

    /// <summary>
    /// Broadcasts a packet to player sessions within a sector-based radius on a map.
    /// </summary>
    /// <param name="packet">Packet to enqueue.</param>
    /// <param name="mapId">Map id.</param>
    /// <param name="centerSectorX">Center sector X coordinate.</param>
    /// <param name="centerSectorY">Center sector Y coordinate.</param>
    /// <param name="sectorRadius">Sector radius (0 = center sector only).</param>
    /// <param name="excludeSessionId">Optional session id to exclude.</param>
    /// <returns>Number of recipient sessions.</returns>
    Task<int> BroadcastToPlayersInSectorRangeAsync(
        IGameNetworkPacket packet,
        int mapId,
        int centerSectorX,
        int centerSectorY,
        int sectorRadius = 0,
        long? excludeSessionId = null
    )
    {
        var clampedRadius = Math.Max(0, sectorRadius);
        var centerLocation = new Point3D(
            (centerSectorX << MapSectorConsts.SectorShift) + MapSectorConsts.SectorSize / 2,
            (centerSectorY << MapSectorConsts.SectorShift) + MapSectorConsts.SectorSize / 2,
            0
        );
        var tileRange = clampedRadius * MapSectorConsts.SectorSize;

        return BroadcastToPlayersAsync(packet, mapId, centerLocation, tileRange, excludeSessionId);
    }

    /// <summary>
    /// Broadcasts a packet to players in the configured update sector radius around a world location.
    /// </summary>
    /// <param name="packet">Packet to enqueue.</param>
    /// <param name="mapId">Map id.</param>
    /// <param name="location">Center location.</param>
    /// <param name="excludeSessionId">Optional session id to exclude.</param>
    /// <returns>Number of recipient sessions.</returns>
    Task<int> BroadcastToPlayersInUpdateRadiusAsync(
        IGameNetworkPacket packet,
        int mapId,
        Point3D location,
        long? excludeSessionId = null
    )
    {
        var sectorX = location.X >> MapSectorConsts.SectorShift;
        var sectorY = location.Y >> MapSectorConsts.SectorShift;

        return BroadcastToPlayersInSectorRangeAsync(
            packet,
            mapId,
            sectorX,
            sectorY,
            GetUpdateBroadcastSectorRadius(),
            excludeSessionId
        );
    }

    /// <summary>
    /// Returns all currently active sectors loaded in the spatial index.
    /// </summary>
    /// <returns>Loaded sectors across all maps.</returns>
    List<MapSector> GetActiveSectors();

    /// <summary>
    /// Returns all mobiles currently indexed in a square sector range around a center sector.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="centerSectorX">Center sector X coordinate.</param>
    /// <param name="centerSectorY">Center sector Y coordinate.</param>
    /// <param name="radius">
    /// Sector radius (0 = only center sector). Default is <c>2</c>, aligned to player default view range (18 tiles).
    /// </param>
    /// <returns>Mobiles in the sector range.</returns>
    List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2);

    /// <summary>
    /// Resolves music id for a world location.
    /// </summary>
    /// <param name="mapId">Target map id.</param>
    /// <param name="location">Target location.</param>
    /// <returns>Music id, or <c>0</c> when no music mapping exists.</returns>
    int GetMusic(int mapId, Point3D location);

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
    /// Returns all player mobiles currently indexed in the specified sector.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="sectorX">Sector X coordinate.</param>
    /// <param name="sectorY">Sector Y coordinate.</param>
    /// <returns>Players in the sector.</returns>
    List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY);

    /// <summary>
    /// Returns a region by its configured id.
    /// </summary>
    /// <param name="regionId">Region id.</param>
    /// <returns>The matching region or <see langword="null" /> if not found.</returns>
    JsonRegion? GetRegionById(int regionId);

    /// <summary>
    /// Resolves the sector containing the specified world location.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="location">World location.</param>
    /// <returns>The sector when present in the index; otherwise <see langword="null" />.</returns>
    MapSector? GetSectorByLocation(int mapId, Point3D location);

    /// <summary>
    /// Returns current spatial index statistics.
    /// </summary>
    /// <returns>Sector stats.</returns>
    SectorSystemStats GetStats();

    /// <summary>
    /// Returns the configured sector radius for live update broadcasts.
    /// </summary>
    int GetUpdateBroadcastSectorRadius()
        => 3;

    /// <summary>
    /// Moves an item in the spatial index.
    /// </summary>
    /// <param name="item">Item entity.</param>
    /// <param name="mapId">Map id.</param>
    /// <param name="oldLocation">Previous location.</param>
    /// <param name="newLocation">New location.</param>
    void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation);

    /// <summary>
    /// Moves a mobile in the spatial index.
    /// </summary>
    /// <param name="mobile">Mobile entity.</param>
    /// <param name="oldLocation">Previous location.</param>
    /// <param name="newLocation">New location.</param>
    void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation);

    /// <summary>
    /// Removes an entity from the index.
    /// </summary>
    /// <param name="serial">Entity serial.</param>
    void RemoveEntity(Serial serial);

    /// <summary>
    /// Resolves the highest-priority region for a world position.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="location">World location.</param>
    /// <returns>Resolved region or <see langword="null" /> when none matches.</returns>
    JsonRegion? ResolveRegion(int mapId, Point3D location)
        => null;
}
