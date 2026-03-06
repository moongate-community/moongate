using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public class MobileHandler
    : IGameEventListener<MobileAddedInSectorEvent>,
      IGameEventListener<MobilePositionChangedEvent>,
      IGameEventListener<PlayerCharacterLoggedInEvent>,
      IMoongateService
{
    private const int DefaultMobileSyncRange = 18;
    private readonly ISpatialWorldService _spatialWorldService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    private readonly ICharacterService _characterService;
    private readonly ISpeechService _speechService;
    private readonly int _sectorEnterSyncRadius;
    private readonly int _mobileSyncRange;
    private readonly IDispatchEventsService _dispatchEventsService;

    public MobileHandler(
        ISpatialWorldService spatialWorldService,
        ICharacterService characterService,
        ISpeechService speechService,
        IDispatchEventsService dispatchEventsService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue,
        MoongateConfig moongateConfig
    )
    {
        _spatialWorldService = spatialWorldService;
        _characterService = characterService;
        _speechService = speechService;
        _dispatchEventsService = dispatchEventsService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _sectorEnterSyncRadius = Math.Max(0, moongateConfig.Spatial.SectorEnterSyncRadius);
        _mobileSyncRange = Math.Max(
            DefaultMobileSyncRange,
            _spatialWorldService.GetUpdateBroadcastSectorRadius() * MapSectorConsts.SectorSize
        );
    }

    public async Task HandleAsync(MobileAddedInSectorEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var mobileEntity = await _characterService.GetCharacterAsync(gameEvent.MobileId);

        if (mobileEntity is null)
        {
            return;
        }

        await UpdatePlayerForMobileMovedOrCreated(
            mobileEntity,
            gameEvent.MapId,
            true
        );
    }

    public async Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var mobileEntity = TryResolveMobileFromSpatial(gameEvent) ??
                           await _characterService.GetCharacterAsync(gameEvent.MobileId);

        if (mobileEntity is null)
        {
            return;
        }

        var sectorInfo = _spatialWorldService.GetSectorByLocation(gameEvent.MapId, gameEvent.NewLocation);

        if (sectorInfo is null)
        {
            return;
        }

        TrySendMapChangeIfNeeded(mobileEntity, gameEvent);
        await UpdatePlayerForMobileMovedOrCreated(
            mobileEntity,
            gameEvent.MapId,
            false
        );

        await SyncSectorSnapshotForEnteringPlayerAsync(
            mobileEntity,
            gameEvent.MapId,
            gameEvent.OldLocation,
            gameEvent.NewLocation
        );
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var mobileEntity = await _characterService.GetCharacterAsync(gameEvent.CharacterId);

        if (mobileEntity is null)
        {
            return;
        }

        await SyncSectorSnapshotForPlayerAsync(
            mobileEntity,
            mobileEntity.MapId,
            mobileEntity.Location
        );
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    private Task UpdatePlayerForMobileMovedOrCreated(
        UOMobileEntity mobileEntity,
        int mapId,
        bool isNew
    )
        => _dispatchEventsService.DispatchMobileUpdateAsync(mobileEntity, mapId, _mobileSyncRange, isNew);

    private async Task SyncSectorSnapshotForEnteringPlayerAsync(
        UOMobileEntity mobileEntity,
        int mapId,
        Point3D oldLocation,
        Point3D newLocation
    )
    {
        if (!mobileEntity.IsPlayer)
        {
            return;
        }

        var oldSector = _spatialWorldService.GetSectorByLocation(mapId, oldLocation);
        var newSector = _spatialWorldService.GetSectorByLocation(mapId, newLocation);

        if (newSector is null)
        {
            return;
        }

        if (oldSector is not null &&
            oldSector.SectorX == newSector.SectorX &&
            oldSector.SectorY == newSector.SectorY)
        {
            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(mobileEntity.Id, out var session))
        {
            return;
        }

        var itemCount = newSector.GetItems()
                                 .Count(
                                     item => item.ParentContainerId == Serial.Zero &&
                                             item.EquippedMobileId == Serial.Zero
                                 );
        var mobileCount = newSector.GetMobiles()
                                   .Count(mobile => mobile.Id != mobileEntity.Id);

        await _speechService.SendMessageFromServerAsync(
            session,
            $"Sector: {newSector.SectorX} {newSector.SectorY} Items: {itemCount} e Mobiles: {mobileCount}"
        );
        await SyncSectorSnapshotForPlayerAsync(mobileEntity, mapId, newLocation);
    }

    private Task SyncSectorSnapshotForPlayerAsync(
        UOMobileEntity mobileEntity,
        int mapId,
        Point3D centerLocation
    )
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(mobileEntity.Id, out var session))
        {
            return Task.CompletedTask;
        }

        var centerSector = _spatialWorldService.GetSectorByLocation(mapId, centerLocation);

        if (centerSector is null)
        {
            return Task.CompletedTask;
        }

        for (var sectorX = centerSector.SectorX - _sectorEnterSyncRadius;
             sectorX <= centerSector.SectorX + _sectorEnterSyncRadius;
             sectorX++)
        {
            for (var sectorY = centerSector.SectorY - _sectorEnterSyncRadius;
                 sectorY <= centerSector.SectorY + _sectorEnterSyncRadius;
                 sectorY++)
            {
                var targetSector = _spatialWorldService.GetSectorByLocation(
                    mapId,
                    new Point3D(
                        sectorX << MapSectorConsts.SectorShift,
                        sectorY << MapSectorConsts.SectorShift,
                        centerLocation.Z
                    )
                );

                if (targetSector is null)
                {
                    continue;
                }

                foreach (var item in targetSector.GetItems())
                {
                    if (item.ParentContainerId != Serial.Zero ||
                        item.EquippedMobileId != Serial.Zero)
                    {
                        continue;
                    }

                    _outgoingPacketQueue.Enqueue(session.SessionId, new ObjectInformationPacket(item));
                }

                foreach (var otherMobile in targetSector.GetMobiles())
                {
                    if (otherMobile.Id == mobileEntity.Id)
                    {
                        continue;
                    }

                    _outgoingPacketQueue.Enqueue(
                        session.SessionId,
                        new MobileIncomingPacket(mobileEntity, otherMobile, true, false)
                    );
                    _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(otherMobile, 1));
                    WornItemPacketHelper.EnqueueVisibleWornItems(
                        otherMobile,
                        packet => _outgoingPacketQueue.Enqueue(session.SessionId, packet)
                    );
                }
            }
        }

        return Task.CompletedTask;
    }

    private UOMobileEntity? TryResolveMobileFromSpatial(MobilePositionChangedEvent gameEvent)
    {
        var nearby = _spatialWorldService.GetNearbyMobiles(gameEvent.NewLocation, 2, gameEvent.MapId);

        return nearby.FirstOrDefault(mobile => mobile.Id == gameEvent.MobileId);
    }

    private void TrySendMapChangeIfNeeded(UOMobileEntity mobileEntity, MobilePositionChangedEvent gameEvent)
    {
        if (!mobileEntity.IsPlayer || gameEvent.OldMapId == gameEvent.MapId)
        {
            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(mobileEntity.Id, out var session))
        {
            return;
        }

        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            GeneralInformationFactory.CreateSetCursorHueSetMap((byte)gameEvent.MapId)
        );

        var map = Map.GetMap(gameEvent.MapId);
        var mapWidth = (ushort)Math.Clamp(map?.Width ?? 0, ushort.MinValue, ushort.MaxValue);
        var mapHeight = (ushort)Math.Clamp(map?.Height ?? 0, ushort.MinValue, ushort.MaxValue);

        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            new ServerChangePacket(mobileEntity.Location, mapWidth, mapHeight)
        );
    }
}
