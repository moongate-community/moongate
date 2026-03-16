using System.Diagnostics;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Characters;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Utils;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.Characters;

public sealed class PlayerLoginWorldSyncService : IPlayerLoginWorldSyncService
{
    private const int DefaultMobileSyncRange = 18;
    private const int LoginSnapshotSectorRadius = 1;
    private static readonly ILogger Logger = Log.ForContext<PlayerLoginWorldSyncService>();
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly int _mobileSyncRange;

    public PlayerLoginWorldSyncService(
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        MoongateConfig moongateConfig
    )
    {
        _ = moongateConfig;
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _mobileSyncRange = Math.Max(
            DefaultMobileSyncRange,
            _spatialWorldService.GetUpdateBroadcastSectorRadius() * MapSectorConsts.SectorSize
        );
    }

    private sealed class LoginSectorSyncStats
    {
        public int SectorsRequested { get; set; }
        public int SectorsLoaded { get; set; }
        public int ItemsSent { get; set; }
        public int MobilesSent { get; set; }
        public int WornItemsSent { get; set; }

        public void Accumulate(SectorSyncStats stats)
        {
            if (stats.SectorLoaded)
            {
                SectorsLoaded++;
            }

            ItemsSent += stats.ItemsSent;
            MobilesSent += stats.MobilesSent;
            WornItemsSent += stats.WornItemsSent;
        }
    }

    private sealed class SectorSyncStats
    {
        public bool SectorLoaded { get; set; }
        public int ItemsSent { get; set; }
        public int MobilesSent { get; set; }
        public int WornItemsSent { get; set; }
    }

    public Task SyncAsync(GameSession session, UOMobileEntity mobileEntity, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var totalStopwatch = Stopwatch.StartNew();
        var centerSectorStopwatch = Stopwatch.StartNew();
        var centerSector = _spatialWorldService.GetSectorByLocation(mobileEntity.MapId, mobileEntity.Location);
        centerSectorStopwatch.Stop();

        if (centerSector is null)
        {
            return Task.CompletedTask;
        }

        var lookupStopwatch = Stopwatch.StartNew();
        var loadedSectors = BuildLoadedSectorLookup(mobileEntity.MapId, centerSector);
        lookupStopwatch.Stop();
        var stats = new LoginSectorSyncStats();
        var syncStopwatch = Stopwatch.StartNew();
        var sentItemSerials = new HashSet<Serial>();
        var sentMobileSerials = new HashSet<Serial>();

        for (var sectorX = centerSector.SectorX - LoginSnapshotSectorRadius;
             sectorX <= centerSector.SectorX + LoginSnapshotSectorRadius;
             sectorX++)
        {
            for (var sectorY = centerSector.SectorY - LoginSnapshotSectorRadius;
                 sectorY <= centerSector.SectorY + LoginSnapshotSectorRadius;
                 sectorY++)
            {
                stats.SectorsRequested++;
                stats.Accumulate(
                    SyncLoadedSectorForPlayer(
                        session,
                        mobileEntity,
                        ResolveLoadedSector(loadedSectors, mobileEntity.MapId, sectorX, sectorY, mobileEntity.Location.Z),
                        sentItemSerials,
                        sentMobileSerials
                    )
                );
            }
        }

        RefillVisibleRangeForPlayer(session, mobileEntity, sentItemSerials, sentMobileSerials, stats);
        syncStopwatch.Stop();
        totalStopwatch.Stop();

        if (totalStopwatch.ElapsedMilliseconds >= 50)
        {
            Logger.Warning(
                "Player login sector snapshot session={SessionId} character={CharacterId} total={TotalMs:0.###}ms centerSector={CenterSectorMs:0.###}ms buildLookup={BuildLookupMs:0.###}ms sync={SyncMs:0.###}ms sectorsRequested={SectorsRequested} sectorsLoaded={SectorsLoaded} itemsSent={ItemsSent} mobilesSent={MobilesSent} wornItemsSent={WornItemsSent}",
                session.SessionId,
                mobileEntity.Id,
                totalStopwatch.Elapsed.TotalMilliseconds,
                centerSectorStopwatch.Elapsed.TotalMilliseconds,
                lookupStopwatch.Elapsed.TotalMilliseconds,
                syncStopwatch.Elapsed.TotalMilliseconds,
                stats.SectorsRequested,
                stats.SectorsLoaded,
                stats.ItemsSent,
                stats.MobilesSent,
                stats.WornItemsSent
            );
        }

        return Task.CompletedTask;
    }

    private Dictionary<(int SectorX, int SectorY), MapSector> BuildLoadedSectorLookup(int mapId, MapSector anchorSector)
    {
        var lookup = new Dictionary<(int SectorX, int SectorY), MapSector>();

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            if (sector.MapIndex != mapId)
            {
                continue;
            }

            lookup[(sector.SectorX, sector.SectorY)] = sector;
        }

        lookup[(anchorSector.SectorX, anchorSector.SectorY)] = anchorSector;

        return lookup;
    }

    private void RefillVisibleRangeForPlayer(
        GameSession session,
        UOMobileEntity mobileEntity,
        HashSet<Serial> sentItemSerials,
        HashSet<Serial> sentMobileSerials,
        LoginSectorSyncStats stats
    )
    {
        foreach (var item in _spatialWorldService.GetNearbyItems(
                     mobileEntity.Location,
                     _mobileSyncRange,
                     mobileEntity.MapId
                 ))
        {
            if (item.ParentContainerId != Serial.Zero ||
                item.EquippedMobileId != Serial.Zero ||
                !ItemVisibilityHelper.CanSessionSeeItem(session, item) ||
                !sentItemSerials.Add(item.Id))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, ItemPacketHelper.CreateObjectInformationPacket(item, session));
            stats.ItemsSent++;
        }

        foreach (var otherMobile in _spatialWorldService.GetNearbyMobiles(
                     mobileEntity.Location,
                     _mobileSyncRange,
                     mobileEntity.MapId
                 ))
        {
            if (otherMobile.Id == mobileEntity.Id || !sentMobileSerials.Add(otherMobile.Id))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(
                session.SessionId,
                new MobileIncomingPacket(mobileEntity, otherMobile, true, false)
            );
            _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(otherMobile, 1));
            stats.MobilesSent++;
            WornItemPacketHelper.EnqueueVisibleWornItems(
                otherMobile,
                packet =>
                {
                    _outgoingPacketQueue.Enqueue(session.SessionId, packet);
                    stats.WornItemsSent++;
                }
            );
        }
    }

    private MapSector? ResolveLoadedSector(
        Dictionary<(int SectorX, int SectorY), MapSector> loadedSectors,
        int mapId,
        int sectorX,
        int sectorY,
        int z
    )
    {
        if (loadedSectors.TryGetValue((sectorX, sectorY), out var sector))
        {
            return sector;
        }

        sector = _spatialWorldService.GetSectorByLocation(
            mapId,
            new(
                sectorX << MapSectorConsts.SectorShift,
                sectorY << MapSectorConsts.SectorShift,
                z
            )
        );

        if (sector is not null)
        {
            loadedSectors[(sectorX, sectorY)] = sector;
        }

        return sector;
    }

    private SectorSyncStats SyncLoadedSectorForPlayer(
        GameSession session,
        UOMobileEntity mobileEntity,
        MapSector? targetSector,
        HashSet<Serial> sentItemSerials,
        HashSet<Serial> sentMobileSerials
    )
    {
        var stats = new SectorSyncStats();

        if (targetSector is null)
        {
            return stats;
        }

        stats.SectorLoaded = true;

        foreach (var item in targetSector.GetItems())
        {
            if (item.ParentContainerId != Serial.Zero ||
                item.EquippedMobileId != Serial.Zero ||
                !ItemVisibilityHelper.CanSessionSeeItem(session, item) ||
                !sentItemSerials.Add(item.Id))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, ItemPacketHelper.CreateObjectInformationPacket(item, session));
            stats.ItemsSent++;
        }

        foreach (var otherMobile in targetSector.GetMobiles())
        {
            if (otherMobile.Id == mobileEntity.Id || !sentMobileSerials.Add(otherMobile.Id))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(
                session.SessionId,
                new MobileIncomingPacket(mobileEntity, otherMobile, true, false)
            );
            _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(otherMobile, 1));
            stats.MobilesSent++;
            WornItemPacketHelper.EnqueueVisibleWornItems(
                otherMobile,
                packet =>
                {
                    _outgoingPacketQueue.Enqueue(session.SessionId, packet);
                    stats.WornItemsSent++;
                }
            );
        }

        return stats;
    }
}
