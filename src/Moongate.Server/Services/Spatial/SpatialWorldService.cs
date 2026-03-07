using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Spatial;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Utils;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.Spatial;

/// <summary>
/// Default in-memory spatial world service that orchestrates entity indexing and region resolution.
/// </summary>
[RegisterGameEventListener]
public sealed class SpatialWorldService
    : ISpatialWorldService, ISpatialMetricsSource,
      IGameEventListener<MobilePositionChangedEvent>,
      IGameEventListener<PlayerCharacterLoggedInEvent>,
      IGameEventListener<DropItemToGroundEvent>
{
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly ICharacterService _characterService;
    private readonly IItemService _itemService;
    private readonly MoongateSpatialConfig _spatialConfig;
    private readonly SpatialEntityIndex _entityIndex;
    private readonly SpatialRegionResolver _regionResolver;

    public SpatialWorldService(
        IGameNetworkSessionService gameNetworkSessionService,
        IGameEventBusService gameEventBusService,
        ICharacterService characterService,
        IItemService itemService,
        IMobileService mobileService,
        IOutgoingPacketQueue outgoingPacketQueue,
        MoongateConfig moongateConfig
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _gameEventBusService = gameEventBusService;
        _characterService = characterService;
        _itemService = itemService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _spatialConfig = moongateConfig.Spatial ?? new();
        _entityIndex = new(itemService, mobileService, _spatialConfig, OnMobileAddedToWorld);
        _regionResolver = new();
    }

    public void AddOrUpdateItem(UOItemEntity item, int mapId)
    {
        _entityIndex.AddOrUpdateItem(item, mapId);

        var sectorX = item.Location.X >> MapSectorConsts.SectorShift;
        var sectorY = item.Location.Y >> MapSectorConsts.SectorShift;

        PublishEvent(new ItemAddedInSectorEvent(item.Id, mapId, sectorX, sectorY));
    }

    public void AddOrUpdateMobile(UOMobileEntity mobile)
    {
        var isNew = _entityIndex.AddOrUpdateMobile(mobile);

        if (!isNew)
        {
            return;
        }

        var sectorX = mobile.Location.X >> MapSectorConsts.SectorShift;
        var sectorY = mobile.Location.Y >> MapSectorConsts.SectorShift;
        PublishEvent(new MobileAddedInSectorEvent(mobile.Id, mobile.MapId, sectorX, sectorY));
        OnMobileAddedToWorld(mobile);
    }

    public void AddRegion(JsonRegion region)
        => _regionResolver.AddRegion(region);

    public Task<int> BroadcastToPlayersAsync(
        IGameNetworkPacket packet,
        int mapId,
        Point3D location,
        int? range = null,
        long? excludeSessionId = null
    )
    {
        ArgumentNullException.ThrowIfNull(packet);
        var excludedSession = ResolveExcludedSession(excludeSessionId);

        List<GameSession> recipients;

        if (range.HasValue)
        {
            recipients = GetPlayersInRange(location, Math.Max(0, range.Value), mapId, excludedSession);
        }
        else
        {
            var sectorX = location.X >> MapSectorConsts.SectorShift;
            var sectorY = location.Y >> MapSectorConsts.SectorShift;
            recipients = GetPlayersInSectorRange(
                mapId,
                sectorX,
                sectorY,
                Math.Max(0, _spatialConfig.SectorEnterSyncRadius),
                excludedSession
            );
        }

        foreach (var recipient in recipients)
        {
            _outgoingPacketQueue.Enqueue(recipient.SessionId, packet);
        }

        return Task.FromResult(recipients.Count);
    }

    public Task<int> BroadcastToPlayersInSectorRangeAsync(
        IGameNetworkPacket packet,
        int mapId,
        int centerSectorX,
        int centerSectorY,
        int sectorRadius = 0,
        long? excludeSessionId = null
    )
    {
        ArgumentNullException.ThrowIfNull(packet);
        var excludedSession = ResolveExcludedSession(excludeSessionId);
        var recipients = GetPlayersInSectorRange(
            mapId,
            centerSectorX,
            centerSectorY,
            Math.Max(0, sectorRadius),
            excludedSession
        );

        foreach (var recipient in recipients)
        {
            _outgoingPacketQueue.Enqueue(recipient.SessionId, packet);
        }

        return Task.FromResult(recipients.Count);
    }

    public Task<int> BroadcastToPlayersInUpdateRadiusAsync(
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

    public List<MapSector> GetActiveSectors()
        => _entityIndex.GetActiveSectors();

    public SectorSystemStats GetMetricsSnapshot()
        => GetStats();

    public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        => _entityIndex.GetMobilesInSectorRange(mapId, centerSectorX, centerSectorY, radius);

    public int GetMusic(int mapId, Point3D location)
        => _regionResolver.GetMusic(mapId, location);

    public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        => _entityIndex.GetNearbyItems(location, range, mapId);

    public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        => _entityIndex.GetNearbyMobiles(location, range, mapId);

    public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
    {
        var players = GetNearbyMobiles(location, range, mapId).Where(static mobile => mobile.IsPlayer);
        var sessionsByCharacter = BuildSessionsByCharacterMap(excludeSession);
        var result = new List<GameSession>();

        foreach (var player in players)
        {
            if (sessionsByCharacter.TryGetValue(player.Id, out var session))
            {
                result.Add(session);
            }
        }

        return result;
    }

    public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        => _entityIndex.GetPlayersInSector(mapId, sectorX, sectorY);

    public JsonRegion? GetRegionById(int regionId)
        => _regionResolver.GetRegionById(regionId);

    public MapSector? GetSectorByLocation(int mapId, Point3D location)
        => _entityIndex.GetSectorByLocation(mapId, location);

    public SectorSystemStats GetStats()
        => _entityIndex.GetStats();

    public int GetUpdateBroadcastSectorRadius()
        => Math.Max(0, _spatialConfig.SectorUpdateBroadcastRadius);

    public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) &&
                session.CharacterId == gameEvent.MobileId)
            {
                OnMobileMoved(session.Character!, gameEvent.OldLocation, gameEvent.NewLocation);

                return Task.CompletedTask;
            }

            return HandleNpcPositionChangedAsync(gameEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var character = ResolveCharacterFromSession(gameEvent.SessionId, gameEvent.CharacterId) ??
                        await _characterService.GetCharacterAsync(gameEvent.CharacterId);

        if (character is null)
        {
            return;
        }

        var sectorX = character.Location.X >> MapSectorConsts.SectorShift;
        var sectorY = character.Location.Y >> MapSectorConsts.SectorShift;
        AddOrUpdateMobile(character);
        WarmupSectorsFireAndForget(character.MapId, sectorX, sectorY);

        var region = _regionResolver.ResolveRegion(character.MapId, character.Location);

        if (region is not null)
        {
            PublishEvent(new PlayerEnteredRegionEvent(character.Id, character.MapId, region.Id, region.Name));
        }
    }

    public async Task HandleAsync(DropItemToGroundEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var item = await _itemService.GetItemAsync(gameEvent.ItemId);

        if (item is null)
        {
            return;
        }

        var mapId = 0;

        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var runtimeSession) &&
            runtimeSession.Character is not null &&
            runtimeSession.CharacterId == gameEvent.MobileId)
        {
            mapId = runtimeSession.Character.MapId;
        }
        else
        {
            var character = await _characterService.GetCharacterAsync(gameEvent.MobileId);

            if (character is null)
            {
                return;
            }

            mapId = character.MapId;
        }

        var sector = GetSectorByLocation(mapId, gameEvent.NewLocation);

        if (sector is null)
        {
            return;
        }

        var range = GetUpdateBroadcastSectorRadius() * MapSectorConsts.SectorSize;
        var sessions = GetPlayersInRange(gameEvent.NewLocation, range, mapId);

        foreach (var session in sessions)
        {
            if (!ItemVisibilityHelper.CanSessionSeeItem(session, item))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, new ObjectInformationPacket(item));
        }
    }

    public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        => _entityIndex.MoveItem(item, mapId, oldLocation, newLocation);

    public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
    {
        var oldRegion = _regionResolver.ResolveRegion(mobile.MapId, oldLocation);
        var newRegion = _regionResolver.ResolveRegion(mobile.MapId, newLocation);
        var move = _entityIndex.MoveMobile(mobile, oldLocation, newLocation);

        if (move.SectorChanged)
        {
            PublishEvent(
                new MobileSectorChangedEvent(
                    mobile.Id,
                    move.MapId,
                    move.OldSectorX,
                    move.OldSectorY,
                    move.NewSectorX,
                    move.NewSectorY
                )
            );

            if (mobile.IsPlayer)
            {
                WarmupSectorsFireAndForget(move.MapId, move.NewSectorX, move.NewSectorY);
            }
        }

        if (!mobile.IsPlayer)
        {
            return;
        }

        var oldRegionId = oldRegion?.Id;
        var newRegionId = newRegion?.Id;

        if (oldRegionId == newRegionId)
        {
            return;
        }

        if (oldRegion is not null)
        {
            PublishEvent(new PlayerExitedRegionEvent(mobile.Id, mobile.MapId, oldRegion.Id, oldRegion.Name));
        }

        if (newRegion is not null)
        {
            PublishEvent(new PlayerEnteredRegionEvent(mobile.Id, mobile.MapId, newRegion.Id, newRegion.Name));
        }
    }

    public void RemoveEntity(Serial serial)
        => _entityIndex.RemoveEntity(serial);

    public JsonRegion? ResolveRegion(int mapId, Point3D location)
        => _regionResolver.ResolveRegion(mapId, location);

    private Dictionary<Serial, GameSession> BuildSessionsByCharacterMap(GameSession? excludeSession)
    {
        var sessions = _gameNetworkSessionService.GetAll();
        var map = new Dictionary<Serial, GameSession>();

        foreach (var session in sessions)
        {
            if (session.Character is not null && session != excludeSession)
            {
                map[session.Character.Id] = session;
            }
        }

        return map;
    }

    private List<GameSession> GetPlayersInSectorRange(
        int mapId,
        int centerSectorX,
        int centerSectorY,
        int sectorRadius,
        GameSession? excludeSession
    )
    {
        var sessionsByCharacter = BuildSessionsByCharacterMap(excludeSession);
        var result = new List<GameSession>();
        var seenSessionIds = new HashSet<long>();

        for (var sectorX = centerSectorX - sectorRadius; sectorX <= centerSectorX + sectorRadius; sectorX++)
        {
            for (var sectorY = centerSectorY - sectorRadius; sectorY <= centerSectorY + sectorRadius; sectorY++)
            {
                var players = GetPlayersInSector(mapId, sectorX, sectorY);

                foreach (var player in players)
                {
                    if (!sessionsByCharacter.TryGetValue(player.Id, out var session) ||
                        !seenSessionIds.Add(session.SessionId))
                    {
                        continue;
                    }

                    result.Add(session);
                }
            }
        }

        return result;
    }

    private async Task HandleNpcPositionChangedAsync(
        MobilePositionChangedEvent gameEvent,
        CancellationToken cancellationToken
    )
    {
        var mobile = TryResolveMobileFromSpatial(gameEvent.MobileId);

        if (mobile is null)
        {
            mobile = await _characterService.GetCharacterAsync(gameEvent.MobileId);
        }

        if (mobile is null)
        {
            return;
        }

        mobile.MapId = gameEvent.MapId;
        OnMobileMoved(mobile, gameEvent.OldLocation, gameEvent.NewLocation);
    }

    private void OnMobileAddedToWorld(UOMobileEntity mobile)
        => PublishEvent(new MobileAddedInWorldEvent(mobile, mobile.BrainId));

    private void PublishEvent<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
    {
        var task = _gameEventBusService.PublishAsync(gameEvent);

        if (!task.IsCompletedSuccessfully)
        {
            task.AsTask()
                .ContinueWith(
                    static t => Log.ForContext<SpatialWorldService>()
                                   .Error(t.Exception, "Event publish failed for {EventType}", typeof(TEvent).Name),
                    TaskContinuationOptions.OnlyOnFaulted
                );
        }
    }

    private UOMobileEntity? ResolveCharacterFromSession(long sessionId, Serial characterId)
    {
        if (_gameNetworkSessionService.TryGet(sessionId, out var session) &&
            session.Character is not null &&
            session.CharacterId == characterId)
        {
            return session.Character;
        }

        return null;
    }

    private GameSession? ResolveExcludedSession(long? excludeSessionId)
    {
        if (!excludeSessionId.HasValue)
        {
            return null;
        }

        return _gameNetworkSessionService.TryGet(excludeSessionId.Value, out var session)
                   ? session
                   : null;
    }

    private UOMobileEntity? TryResolveMobileFromSpatial(Serial mobileId)
        => _entityIndex.TryGetEntity<UOMobileEntity>(mobileId);

    private void WarmupSectorsFireAndForget(int mapId, int sectorX, int sectorY)
    {
        var warmupRadius = Math.Max(0, _spatialConfig.SectorWarmupRadius);
        var task = _entityIndex.WarmupAroundSectorAsync(mapId, sectorX, sectorY, warmupRadius, CancellationToken.None);

        if (!task.IsCompletedSuccessfully)
        {
            task.ContinueWith(
                static t => Log.ForContext<SpatialWorldService>()
                               .Error(t.Exception, "Sector warmup failed"),
                TaskContinuationOptions.OnlyOnFaulted
            );
        }
    }
}
