using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Utils;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
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
    private const int NearEdgePreloadThreshold = 4;
    private readonly ISpatialWorldService _spatialWorldService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    private readonly ICharacterService _characterService;
    private readonly ILightService? _lightService;
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
        MoongateConfig moongateConfig,
        ILightService? lightService = null
    )
    {
        _spatialWorldService = spatialWorldService;
        _characterService = characterService;
        _lightService = lightService;
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
        var mobileEntity = ResolveUpdatedCharacterFromSession(gameEvent) ??
                           TryResolveMobileFromSpatial(gameEvent) ??
                           await _characterService.GetCharacterAsync(gameEvent.MobileId);

        if (mobileEntity is null)
        {
            return;
        }

        var isPlayerCrossMap = mobileEntity.IsPlayer && gameEvent.OldMapId != gameEvent.MapId;

        if (gameEvent.IsTeleport && !isPlayerCrossMap)
        {
            await SendTeleportSourceEffectsAsync(mobileEntity, gameEvent);
        }

        await TrySendMapChangeIfNeededAsync(mobileEntity, gameEvent);

        if (gameEvent.IsTeleport && isPlayerCrossMap)
        {
            await SendTeleportSourceEffectsAsync(mobileEntity, gameEvent);
        }

        var sectorInfo = _spatialWorldService.GetSectorByLocation(gameEvent.MapId, gameEvent.NewLocation);

        if (sectorInfo is null)
        {
            return;
        }
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

        if (gameEvent.IsTeleport)
        {
            await SendTeleportDestinationEffectsAsync(mobileEntity, gameEvent);
        }
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var mobileEntity = ResolveCharacterFromSession(gameEvent.SessionId, gameEvent.CharacterId) ??
                           await _characterService.GetCharacterAsync(gameEvent.CharacterId);

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

    private UOMobileEntity? ResolveCharacterFromSession(long sessionId, Serial characterId)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(characterId, out var session) &&
            session.Character is not null &&
            session.SessionId == sessionId)
        {
            return session.Character;
        }

        return null;
    }

    private UOMobileEntity? ResolveUpdatedCharacterFromSession(MobilePositionChangedEvent gameEvent)
    {
        var sessionCharacter = ResolveCharacterFromSession(gameEvent.SessionId, gameEvent.MobileId);

        if (sessionCharacter is null)
        {
            return null;
        }

        if (sessionCharacter.MapId != gameEvent.MapId || sessionCharacter.Location != gameEvent.NewLocation)
        {
            return null;
        }

        return sessionCharacter;
    }

    private async Task SendTeleportDestinationEffectsAsync(UOMobileEntity mobileEntity, MobilePositionChangedEvent gameEvent)
    {
        await SendTeleportPacketAsync(
            mobileEntity,
            gameEvent.SessionId,
            gameEvent.MapId,
            gameEvent.NewLocation,
            TeleportEffectUtils.CreateDestinationEffect(gameEvent.NewLocation),
            includeSelf: true
        );
        await SendTeleportPacketAsync(
            mobileEntity,
            gameEvent.SessionId,
            gameEvent.MapId,
            gameEvent.NewLocation,
            TeleportEffectUtils.CreateDestinationSound(gameEvent.NewLocation),
            includeSelf: true
        );
    }

    private Task SendTeleportSourceEffectsAsync(UOMobileEntity mobileEntity, MobilePositionChangedEvent gameEvent)
        => SendTeleportPacketAsync(
            mobileEntity,
            gameEvent.SessionId,
            gameEvent.OldMapId,
            gameEvent.OldLocation,
            TeleportEffectUtils.CreateSourceEffect(gameEvent.OldLocation),
            includeSelf: gameEvent.OldMapId == gameEvent.MapId
        );

    private async Task SendTeleportPacketAsync(
        UOMobileEntity mobileEntity,
        long sessionId,
        int mapId,
        Point3D location,
        IGameNetworkPacket packet,
        bool includeSelf
    )
    {
        await _spatialWorldService.BroadcastToPlayersAsync(
            packet,
            mapId,
            location,
            excludeSessionId: sessionId >= 0 ? sessionId : null
        );

        if (!includeSelf)
        {
            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(mobileEntity.Id, out var session))
        {
            return;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, packet);
    }

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

        if (!_gameNetworkSessionService.TryGetByCharacterId(mobileEntity.Id, out var session))
        {
            return;
        }

        var sectorChanged = oldSector is null ||
                            oldSector.SectorX != newSector.SectorX ||
                            oldSector.SectorY != newSector.SectorY;
        HashSet<(int SectorX, int SectorY)> nearEdgeSectors = [];

        if (!sectorChanged && !TryCollectNearEdgePreloadSectors(newSector, newLocation, out nearEdgeSectors))
        {
            return;
        }

        if (!sectorChanged)
        {
            foreach (var (sectorX, sectorY) in nearEdgeSectors)
            {
                SyncSingleSectorForPlayer(session.SessionId, mobileEntity, mapId, sectorX, sectorY, newLocation.Z);
            }

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

        for (var sectorX = newSector.SectorX - _sectorEnterSyncRadius;
             sectorX <= newSector.SectorX + _sectorEnterSyncRadius;
             sectorX++)
        {
            for (var sectorY = newSector.SectorY - _sectorEnterSyncRadius;
                 sectorY <= newSector.SectorY + _sectorEnterSyncRadius;
                 sectorY++)
            {
                SyncSingleSectorForPlayer(session.SessionId, mobileEntity, mapId, sectorX, sectorY, newLocation.Z);
            }
        }
    }

    private bool TryCollectNearEdgePreloadSectors(
        MapSector sector,
        Point3D location,
        out HashSet<(int SectorX, int SectorY)> sectors
    )
    {
        sectors = [];

        var sectorOriginX = sector.SectorX << MapSectorConsts.SectorShift;
        var sectorOriginY = sector.SectorY << MapSectorConsts.SectorShift;
        var localX = location.X - sectorOriginX;
        var localY = location.Y - sectorOriginY;
        var nearWest = localX < NearEdgePreloadThreshold;
        var nearEast = localX >= MapSectorConsts.SectorSize - NearEdgePreloadThreshold;
        var nearNorth = localY < NearEdgePreloadThreshold;
        var nearSouth = localY >= MapSectorConsts.SectorSize - NearEdgePreloadThreshold;

        if (!nearWest && !nearEast && !nearNorth && !nearSouth)
        {
            return false;
        }

        var minX = sector.SectorX - _sectorEnterSyncRadius;
        var maxX = sector.SectorX + _sectorEnterSyncRadius;
        var minY = sector.SectorY - _sectorEnterSyncRadius;
        var maxY = sector.SectorY + _sectorEnterSyncRadius;

        if (nearWest)
        {
            var preloadX = minX - 1;

            for (var sectorY = minY; sectorY <= maxY; sectorY++)
            {
                sectors.Add((preloadX, sectorY));
            }
        }

        if (nearEast)
        {
            var preloadX = maxX + 1;

            for (var sectorY = minY; sectorY <= maxY; sectorY++)
            {
                sectors.Add((preloadX, sectorY));
            }
        }

        if (nearNorth)
        {
            var preloadY = minY - 1;

            for (var sectorX = minX; sectorX <= maxX; sectorX++)
            {
                sectors.Add((sectorX, preloadY));
            }
        }

        if (nearSouth)
        {
            var preloadY = maxY + 1;

            for (var sectorX = minX; sectorX <= maxX; sectorX++)
            {
                sectors.Add((sectorX, preloadY));
            }
        }

        if (nearWest && nearNorth)
        {
            sectors.Add((minX - 1, minY - 1));
        }

        if (nearWest && nearSouth)
        {
            sectors.Add((minX - 1, maxY + 1));
        }

        if (nearEast && nearNorth)
        {
            sectors.Add((maxX + 1, minY - 1));
        }

        if (nearEast && nearSouth)
        {
            sectors.Add((maxX + 1, maxY + 1));
        }

        return sectors.Count > 0;
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
                SyncSingleSectorForPlayer(session.SessionId, mobileEntity, mapId, sectorX, sectorY, centerLocation.Z);
            }
        }

        return Task.CompletedTask;
    }

    private void SyncSingleSectorForPlayer(
        long sessionId,
        UOMobileEntity mobileEntity,
        int mapId,
        int sectorX,
        int sectorY,
        int z
    )
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out var session))
        {
            return;
        }

        var targetSector = _spatialWorldService.GetSectorByLocation(
            mapId,
            new(
                sectorX << MapSectorConsts.SectorShift,
                sectorY << MapSectorConsts.SectorShift,
                z
            )
        );

        if (targetSector is null)
        {
            return;
        }

        foreach (var item in targetSector.GetItems())
        {
            if (item.ParentContainerId != Serial.Zero ||
                item.EquippedMobileId != Serial.Zero ||
                !ItemVisibilityHelper.CanSessionSeeItem(session, item))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(sessionId, ItemPacketHelper.CreateObjectInformationPacket(item, session));
        }

        foreach (var otherMobile in targetSector.GetMobiles())
        {
            if (otherMobile.Id == mobileEntity.Id)
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(
                sessionId,
                new MobileIncomingPacket(mobileEntity, otherMobile, true, false)
            );
            _outgoingPacketQueue.Enqueue(sessionId, new PlayerStatusPacket(otherMobile, 1));
            WornItemPacketHelper.EnqueueVisibleWornItems(
                otherMobile,
                packet => _outgoingPacketQueue.Enqueue(sessionId, packet)
            );
        }
    }

    private UOMobileEntity? TryResolveMobileFromSpatial(MobilePositionChangedEvent gameEvent)
    {
        var nearby = _spatialWorldService.GetNearbyMobiles(gameEvent.NewLocation, 2, gameEvent.MapId);

        return nearby.FirstOrDefault(mobile => mobile.Id == gameEvent.MobileId);
    }

    private async Task TrySendMapChangeIfNeededAsync(UOMobileEntity mobileEntity, MobilePositionChangedEvent gameEvent)
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
        _outgoingPacketQueue.Enqueue(session.SessionId, new DrawPlayerPacket(mobileEntity));
        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            new MobileDrawPacket(mobileEntity, mobileEntity, true, true)
        );
        WornItemPacketHelper.EnqueueVisibleWornItems(
            mobileEntity,
            packet => _outgoingPacketQueue.Enqueue(session.SessionId, packet)
        );
        _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(mobileEntity, 1));

        var globalLight = _lightService?.ComputeGlobalLightLevel(gameEvent.MapId, mobileEntity.Location) ??
                          (int)LightLevelType.Day;
        var globalLightLevel = (LightLevelType)(byte)Math.Clamp(globalLight, 0, byte.MaxValue);
        var personalLightLevel = (LightLevelType)0;
        _outgoingPacketQueue.Enqueue(session.SessionId, new OverallLightLevelPacket(globalLightLevel));
        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            new PersonalLightLevelPacket(personalLightLevel, mobileEntity)
        );

        var season = map?.Season ?? mobileEntity.Map?.Season ?? SeasonType.Spring;
        _outgoingPacketQueue.Enqueue(session.SessionId, new SeasonPacket(season));

        _outgoingPacketQueue.Enqueue(session.SessionId, new PaperdollPacket(mobileEntity));
        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            new SetMusicPacket(_spatialWorldService.GetMusic(gameEvent.MapId, mobileEntity.Location))
        );
        await EnqueueBackpackAsync(session.SessionId, mobileEntity);
        SendOldRangeDeletes(session, mobileEntity.Id, gameEvent.OldMapId, gameEvent.OldLocation);
    }

    private async Task EnqueueBackpackAsync(long sessionId, UOMobileEntity mobileEntity)
    {
        var backpack = await _characterService.GetBackpackWithItemsAsync(mobileEntity);

        if (backpack is null)
        {
            return;
        }

        _outgoingPacketQueue.Enqueue(sessionId, new DrawContainerAndAddItemCombinedPacket(backpack));
    }

    private void SendOldRangeDeletes(GameSession session, Serial playerId, int oldMapId, Point3D oldLocation)
    {
        var deletedSerials = new HashSet<Serial>();

        foreach (var mobile in _spatialWorldService.GetNearbyMobiles(oldLocation, _mobileSyncRange, oldMapId))
        {
            if (mobile.Id == playerId || !deletedSerials.Add(mobile.Id))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, new DeleteObjectPacket(mobile.Id));
        }

        foreach (var item in _spatialWorldService.GetNearbyItems(oldLocation, _mobileSyncRange, oldMapId))
        {
            if (item.ParentContainerId != Serial.Zero ||
                item.EquippedMobileId != Serial.Zero ||
                !ItemVisibilityHelper.CanSessionSeeItem(session, item) ||
                !deletedSerials.Add(item.Id))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, new DeleteObjectPacket(item.Id));
        }
    }

    private Task UpdatePlayerForMobileMovedOrCreated(
        UOMobileEntity mobileEntity,
        int mapId,
        bool isNew
    )
        => _dispatchEventsService.DispatchMobileUpdateAsync(mobileEntity, mapId, _mobileSyncRange, isNew);
}
