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
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

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
    private readonly IMobileService _mobileService;
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
        _mobileService = mobileService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _spatialConfig = moongateConfig.Spatial ?? new();
        _entityIndex = new(itemService, mobileService, _spatialConfig);
        _regionResolver = new();
    }

    public void AddOrUpdateItem(UOItemEntity item, int mapId)
        => _entityIndex.AddOrUpdateItem(item, mapId);

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
    }

    public void AddRegion(JsonRegion region)
        => _regionResolver.AddRegion(region);

    public JsonRegion? GetRegionById(int regionId)
        => _regionResolver.GetRegionById(regionId);

    public int GetMusic(int mapId, Point3D location)
        => _regionResolver.GetMusic(mapId, location);

    public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        => _entityIndex.GetNearbyItems(location, range, mapId);

    public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        => _entityIndex.GetNearbyMobiles(location, range, mapId);

    public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
    {
        var players = GetNearbyMobiles(location, range, mapId).Where(static mobile => mobile.IsPlayer).ToList();
        var sessions = _gameNetworkSessionService.GetAll();
        var sessionsByCharacter = sessions
                                  .Where(static session => session.Character is not null)
                                  .ToDictionary(static session => session.Character!.Id, static session => session);
        var result = new List<GameSession>();

        foreach (var player in players)
        {
            if (sessionsByCharacter.TryGetValue(player.Id, out var session) &&
                session != excludeSession)
            {
                result.Add(session);
            }
        }

        return result;
    }

    public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        => _entityIndex.GetPlayersInSector(mapId, sectorX, sectorY);

    public List<MapSector> GetActiveSectors()
        => _entityIndex.GetActiveSectors();

    public MapSector? GetSectorByLocation(int mapId, Point3D location)
        => _entityIndex.GetSectorByLocation(mapId, location);

    public SectorSystemStats GetStats()
        => _entityIndex.GetStats();

    public SectorSystemStats GetMetricsSnapshot()
        => GetStats();

    public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session) &&
                session.CharacterId == gameEvent.MobileId)
            {
                OnMobileMoved(session.Character!, gameEvent.OldLocation, gameEvent.NewLocation);
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var character = await _characterService.GetCharacterAsync(gameEvent.CharacterId);
        var sectorX = character.Location.X >> MapSectorConsts.SectorShift;
        var sectorY = character.Location.Y >> MapSectorConsts.SectorShift;
        var warmupRadius = Math.Max(0, _spatialConfig.SectorWarmupRadius);

        await _entityIndex.WarmupAroundSectorAsync(character.MapId, sectorX, sectorY, warmupRadius, cancellationToken);

        AddOrUpdateMobile(character);

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

        var players = GetPlayersInSector(mapId, sector.SectorX, sector.SectorY);
        var dropPacket = new ObjectInformationPacket(item);

        foreach (var player in players)
        {
            if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
            {
                _outgoingPacketQueue.Enqueue(session.SessionId, dropPacket);
            }
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

    private void PublishEvent<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        => _gameEventBusService.PublishAsync(gameEvent).AsTask().GetAwaiter().GetResult();
}
