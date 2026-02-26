using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Handlers;

public class MobileHandler
    : IGameEventListener<MobileAddedInSectorEvent>, IGameEventListener<MobilePositionChangedEvent>, IMoongateService
{
    private readonly IGameEventBusService _gameEventBusService;

    private readonly ISpatialWorldService _spatialWorldService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    private readonly ICharacterService _characterService;

    public MobileHandler(
        IGameEventBusService gameEventBusService,
        ISpatialWorldService spatialWorldService,
        ICharacterService characterService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _gameEventBusService = gameEventBusService;
        _spatialWorldService = spatialWorldService;
        _characterService = characterService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task HandleAsync(MobileAddedInSectorEvent gameEvent, CancellationToken cancellationToken = default)
    {
        var mobileEntity = await _characterService.GetCharacterAsync(gameEvent.MobileId);

        if (mobileEntity is null)
        {
            return;
        }

        await UpdatePlayerForMobileMovedOrCreated(
            mobileEntity,
            gameEvent.MapId,
            gameEvent.SectorX,
            gameEvent.SectorY,
            true
        );
    }

    private Task UpdatePlayerForMobileMovedOrCreated(
        UOMobileEntity mobileEntity,
        int mapId,
        int sectorX,
        int sectorY,
        bool isNew
    )
    {
        var players = _spatialWorldService.GetPlayersInSector(mapId, sectorX, sectorY);

        foreach (var player in players)
        {
            if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
            {
                var packet = new MobileIncomingPacket(player, mobileEntity, true, isNew);

                _outgoingPacketQueue.Enqueue(session.SessionId, packet);
            }
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var mobileEntity = _characterService.GetCharacterAsync(gameEvent.MobileId).GetAwaiter().GetResult();

        if (mobileEntity is null)
        {
            return Task.CompletedTask;
        }

        var sectorInfo = _spatialWorldService.GetSectorByLocation(gameEvent.MapId, gameEvent.NewLocation);
        if (sectorInfo is null)
        {
            return Task.CompletedTask;
        }

        return UpdatePlayerForMobileMovedOrCreated(
            mobileEntity,
            gameEvent.MapId,
            sectorInfo.SectorX,
            sectorInfo.SectorY,
            false
        );
    }

    public Task StartAsync()
    {
        _gameEventBusService.RegisterListener<MobileAddedInSectorEvent>(this);
        _gameEventBusService.RegisterListener<MobilePositionChangedEvent>(this);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
