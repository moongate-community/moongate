using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public class MobileHandler
    : IGameEventListener<MobileAddedInSectorEvent>, IGameEventListener<MobilePositionChangedEvent>, IMoongateService
{
    private readonly ISpatialWorldService _spatialWorldService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    private readonly ICharacterService _characterService;

    public MobileHandler(
        ISpatialWorldService spatialWorldService,
        ICharacterService characterService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _spatialWorldService = spatialWorldService;
        _characterService = characterService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
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
            gameEvent.SectorX,
            gameEvent.SectorY,
            true
        );
    }

    public async Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var mobileEntity = await _characterService.GetCharacterAsync(gameEvent.MobileId);

        if (mobileEntity is null)
        {
            return;
        }

        var sectorInfo = _spatialWorldService.GetSectorByLocation(gameEvent.MapId, gameEvent.NewLocation);

        if (sectorInfo is null)
        {
            return;
        }

        await UpdatePlayerForMobileMovedOrCreated(
            mobileEntity,
            gameEvent.MapId,
            sectorInfo.SectorX,
            sectorInfo.SectorY,
            false
        );
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

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
            if (player.Id == mobileEntity.Id)
            {
                continue;
            }

            if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
            {
                _outgoingPacketQueue.Enqueue(session.SessionId, new MobileIncomingPacket(player, mobileEntity, true, isNew));

                //  _outgoingPacketQueue.Enqueue(session.SessionId, new DrawPlayerPacket(mobileEntity));
                _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(mobileEntity, 1));

                WornItemPacketHelper.EnqueueVisibleWornItems(
                    mobileEntity,
                    packet => _outgoingPacketQueue.Enqueue(session.SessionId, packet)
                );

                // _outgoingPacketQueue.Enqueue(session.SessionId, new MobileDrawPacket(player, mobileEntity, true, isNew));
            }
        }

        return Task.CompletedTask;
    }
}
