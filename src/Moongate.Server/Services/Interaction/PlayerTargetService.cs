using System.Collections.Concurrent;
using Moongate.Abstractions.Interfaces.Services.Base;
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
using Moongate.Server.Interfaces.Session;
using Moongate.UO.Data.Ids;
using Serilog;

namespace Moongate.Server.Services.Interaction;

[RegisterGameEventListener, RegisterPacketHandler(PacketDefinition.TargetCursorCommandsPacket)]
public class PlayerTargetService
    : IPlayerTargetService, IPacketListener, IGameEventListener<TargetRequestCursorEvent>, IMoongateService
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

    public async Task HandleAsync(TargetRequestCursorEvent gameEvent, CancellationToken cancellationToken = default)
        => await SendTargetCursorInternalAsync(
               gameEvent.SessionId,
               gameEvent.Callback,
               gameEvent.SelectionType,
               gameEvent.CursorType,
               false
           );

    public async Task<bool> HandlePacketAsync(IGameSession session, IGameNetworkPacket packet)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(packet);

        if (session is not GameSession gameSession)
        {
            throw new InvalidOperationException(
                $"Packet listener '{nameof(PlayerTargetService)}' requires a concrete {nameof(GameSession)}."
            );
        }

        if (packet is TargetCursorCommandsPacket targetCursorCommandsPacket)
        {
            await DispatchCursorResponseAsync(gameSession, targetCursorCommandsPacket);
        }

        return true;
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

    public async Task<Serial> SendTargetCursorAsync(
        long sessionId,
        Action<PendingCursorCallback> callback,
        TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
        TargetCursorType cursorType = TargetCursorType.Neutral
    )
        => await SendTargetCursorInternalAsync(sessionId, callback, selectionType, cursorType, true);

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

    public Task StopAsync()
        => Task.CompletedTask;

    private Task DispatchCursorResponseAsync(GameSession session, TargetCursorCommandsPacket targetCursorResponsePacket)
    {
        if (_pendingCursors.TryRemove(targetCursorResponsePacket.CursorId, out var pendingCursor))
        {
            try
            {
                pendingCursor.Callback(new(targetCursorResponsePacket));
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
}
