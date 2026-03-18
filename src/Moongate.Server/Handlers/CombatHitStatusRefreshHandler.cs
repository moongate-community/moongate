using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class CombatHitStatusRefreshHandler : IGameEventListener<CombatHitEvent>, IMoongateService
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public CombatHitStatusRefreshHandler(
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public Task HandleAsync(CombatHitEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var defender = gameEvent.Defender;

        if (defender.IsPlayer)
        {
            return Task.CompletedTask;
        }

        var candidates = _spatialWorldService.GetPlayersInRange(
            defender.Location,
            MapSectorConsts.MaxViewRange,
            defender.MapId
        );

        foreach (var session in candidates)
        {
            if (session.Character is null ||
                session.Character.MapId != defender.MapId ||
                !session.Character.Location.InRange(defender.Location, session.ViewRange))
            {
                continue;
            }

            _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(defender, 1));
        }

        return Task.CompletedTask;
    }

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
