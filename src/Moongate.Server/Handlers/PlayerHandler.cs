using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public class PlayerHandler : IGameEventListener<PlayerEnteredRegionEvent>, IGameEventListener<PlayerExitedRegionEvent>
{
    private readonly ILogger _logger = Log.ForContext<PlayerHandler>();

    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public PlayerHandler(
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public Task HandleAsync(PlayerEnteredRegionEvent gameEvent, CancellationToken cancellationToken = default)
    {
        return ProcessRegionAsync(gameEvent.MobileId, gameEvent.RegionId);
    }

    public Task HandleAsync(PlayerExitedRegionEvent gameEvent, CancellationToken cancellationToken = default)
    {
        return ProcessRegionAsync(gameEvent.MobileId, gameEvent.RegionId);
    }

    private Task ProcessRegionAsync(Serial mobileId, int regionId)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session))
        {
            var region = _spatialWorldService.GetRegionById(regionId);

            if (region is not null)
            {
                if (region.Music.HasValue)
                {
                    _logger.Debug(
                        "Sending region info {RegionId} with music {MusicId} for session {SessionId}",
                        region.Id,
                        region.Music.Value,
                        session.SessionId
                    );
                    _outgoingPacketQueue.Enqueue(session.SessionId, new SetMusicPacket(region.Music.Value));

                    if (region is JsonTownRegion regionJson)
                    {
                        _outgoingPacketQueue.Enqueue(
                            session.SessionId,
                            SpeechMessageFactory.CreateSystem($"Welcome in {regionJson.Name}", SpeechHues.Green)
                        );

                        if (!regionJson.GuardsDisabled)
                        {
                            _outgoingPacketQueue.Enqueue(
                                session.SessionId,
                                SpeechMessageFactory.CreateSystem("You are protected by the town guards.", SpeechHues.Green)
                            );
                        }
                        else
                        {
                            _outgoingPacketQueue.Enqueue(
                                session.SessionId,
                                SpeechMessageFactory.CreateSystem("You are not protected by the town guards", SpeechHues.Red)
                            );
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
