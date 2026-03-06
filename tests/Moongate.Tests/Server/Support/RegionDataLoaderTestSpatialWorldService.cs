using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Support;

public class RegionDataLoaderTestSpatialWorldService : ISpatialWorldService
{
    public List<JsonRegion> AddedRegions { get; } = [];
    public JsonRegion? ResolvedRegion { get; set; }

    public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

    public void AddOrUpdateMobile(UOMobileEntity mobile) { }

    public void AddRegion(JsonRegion region)
        => AddedRegions.Add(region);

    public Task<int> BroadcastToPlayersAsync(
        IGameNetworkPacket packet,
        int mapId,
        Point3D location,
        int? range = null,
        long? excludeSessionId = null
    )
        => Task.FromResult(0);

    public List<MapSector> GetActiveSectors()
        => [];

    public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
        => [];

    public int GetMusic(int mapId, Point3D location)
        => 0;

    public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        => [];

    public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        => [];

    public virtual List<GameSession> GetPlayersInRange(
        Point3D location,
        int range,
        int mapId,
        GameSession? excludeSession = null
    )
        => [];

    public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        => [];

    public JsonRegion? GetRegionById(int regionId)
        => AddedRegions.FirstOrDefault(region => region.Id == regionId);

    public MapSector? GetSectorByLocation(int mapId, Point3D location)
        => null;

    public SectorSystemStats GetStats()
        => new();

    public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

    public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

    public void RemoveEntity(Serial serial) { }

    public JsonRegion? ResolveRegion(int mapId, Point3D location)
        => ResolvedRegion;
}
