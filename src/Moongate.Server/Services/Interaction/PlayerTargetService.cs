using System.Collections.Concurrent;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Listener;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Ids;
using Serilog;

namespace Moongate.Server.Services.Interaction;

[RegisterGameEventListener]
[RegisterPacketHandler(PacketDefinition.TargetCursorCommandsPacket)]
public class PlayerTargetService : IPlayerTargetService, IPacketListener, IGameEventListener<TargetRequestCursorEvent>
{
    private readonly ILogger _logger = Log.ForContext<PlayerTargetService>();

    private readonly ITimerService _timerService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    private readonly IGameEventBusService _gameEventBusService;

    private readonly ConcurrentDictionary<Serial, PendingCursorObject> _pendingCursors = new();

    public PlayerTargetService(
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameEventBusService gameEventBusService,
        ITimerService timerService
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameEventBusService = gameEventBusService;
        _timerService = timerService;
    }

    public Task StartAsync()
    {
        _timerService.RegisterTimer(
            "pending_cursor_cleanup",
            TimeSpan.FromMinutes(1),
            PendingCleanUpTimerCallback,
            TimeSpan.FromMinutes(1),
            true
        );

        return Task.CompletedTask;
    }

    private void PendingCleanUpTimerCallback()
    {
        var now = DateTime.UtcNow;

        foreach (var kvp in _pendingCursors)
        {
            if (kvp.Value.Expiration < now)
            {
                _logger.Debug("Deleting pending cursor {cursorId}, (maybe the client is dead?)", kvp.Value.SessionId);
                _pendingCursors.TryRemove(kvp.Key, out _);
            }
        }
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<Serial> SendTargetCursorAsync(
        long sessionId,
        Action<PendingCursorCallback> callback,
        TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
        TargetCursorType cursorType = TargetCursorType.Neutral
    )
    {
        return await SendTargetCursorInternalAsync(sessionId, callback, selectionType, cursorType, publishEvent: true);
    }

    private async Task<Serial> SendTargetCursorInternalAsync(
        long sessionId,
        Action<PendingCursorCallback> callback,
        TargetCursorSelectionType selectionType,
        TargetCursorType cursorType,
        bool publishEvent
    )
    {
        if (_gameNetworkSessionService.TryGet(sessionId, out _))
        {
            var randomCursorId = Serial.RandomSerial();
            var targetCursorPacket = new TargetCursorCommandsPacket(selectionType, randomCursorId, cursorType);

            _outgoingPacketQueue.Enqueue(sessionId, targetCursorPacket);

            var pendingCursorObject = new PendingCursorObject(
                sessionId,
                randomCursorId,
                callback,
                DateTime.UtcNow.AddMinutes(5)
            );

            _pendingCursors.TryAdd(randomCursorId, pendingCursorObject);

            if (publishEvent)
            {
                await _gameEventBusService.PublishAsync(
                    new TargetRequestCursorEvent(sessionId, selectionType, cursorType, callback)
                );
            }

            return randomCursorId;
        }

        return Serial.Zero;
    }

    public async Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
    {
        if (_pendingCursors.TryRemove(cursorId, out _))
        {
            var cancelPacket = new TargetCursorCommandsPacket(
                TargetCursorSelectionType.SelectLocation,
                cursorId,
                TargetCursorType.CancelCurrentTargeting
            );
            _outgoingPacketQueue.Enqueue(sessionId, cancelPacket);
        }
    }

    public async Task<bool> HandlePacketAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is TargetCursorCommandsPacket targetCursorCommandsPacket)
        {
            await DispatchCursorResponseAsync(session, targetCursorCommandsPacket);
        }

        return true;
    }

    private Task DispatchCursorResponseAsync(GameSession session, TargetCursorCommandsPacket targetCursorResponsePacket)
    {
        if (_pendingCursors.TryRemove(targetCursorResponsePacket.CursorId, out var pendingCursor))
        {
            try
            {
                pendingCursor.Callback(new PendingCursorCallback(targetCursorResponsePacket));
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error executing pending cursor callback for cursor id {cursorId} in session {sessionId}",
                    targetCursorResponsePacket.CursorId,
                    session.SessionId
                );
            }
        }
        else
        {
            _logger.Warning(
                "Received target cursor response with unknown cursor id {cursorId} from session {sessionId}",
                targetCursorResponsePacket.CursorId,
                session.SessionId
            );
        }

        return Task.CompletedTask;
    }

    public async Task HandleAsync(TargetRequestCursorEvent gameEvent, CancellationToken cancellationToken = default)
    {
        await SendTargetCursorInternalAsync(
            gameEvent.SessionId,
            gameEvent.Callback,
            gameEvent.SelectionType,
            gameEvent.CursorType,
            publishEvent: false
        );
    }
}
