using System.Collections.Frozen;
using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Sessions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Sessions;
using Moongate.Server.Services.Packets.Internal;
using Serilog;

namespace Moongate.Server.Services.Packets;

/// <summary>
///     Dispatches typed handlers through the existing bounded game loop inbox.
/// </summary>
public sealed class PacketDispatchService : IPacketDispatchService, IAsyncDisposable
{
    /// <summary>
    ///     How many packets a session may send while one of its async handlers runs; they wait and run after it, in order.
    ///     A client entering a crowded place asks for the name of every mobile it sees (one 0x09 each) while the
    ///     enter-world handler still runs, so the limit must hold a few hundred; beyond it the session is closed.
    /// </summary>
    public const int MaxPendingPerSession = 1024;

    private readonly PacketRegistry _packets;
    private readonly Lock _gate = new();
    private readonly IGameLoopService _gameLoop;
    private readonly ISessionService _sessions;
    private readonly PacketHandlerRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly Dictionary<long, Task> _disconnects = new();
    private readonly Dictionary<long, Queue<IPacket>> _pending = new();
    private readonly ILogger _logger = Log.ForContext<PacketDispatchService>();

    private FrozenDictionary<Type, Action<GameSession, IPacket>> _handlers =
        FrozenDictionary<Type, Action<GameSession, IPacket>>.Empty;

    private FrozenDictionary<Type, Func<PacketContext, IPacket, CancellationToken, ValueTask>> _asyncHandlers =
        FrozenDictionary<Type, Func<PacketContext, IPacket, CancellationToken, ValueTask>>.Empty;

    private AsyncPacketExecutor? _asyncExecutor;
    private ISessionClosedListener[] _closedListeners = [];

    private bool _everStarted;
    private bool _running;
    private bool _stopped;
    private Task? _stopTask;

    public PacketDispatchService(
        IGameLoopService gameLoop,
        ISessionService sessions,
        PacketHandlerRegistry registry,
        IResolverContext resolver,
        PacketRegistry? packets = null
    )
    {
        _packets = packets ?? PacketRegistry.Default;
        _gameLoop = gameLoop;
        _sessions = sessions;
        _registry = registry;
        _resolver = resolver;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(long sessionId)
    {
        lock (_gate)
        {
            _pending.Remove(sessionId);
        }

        var cancellationFailure = _asyncExecutor?.CancelSession(sessionId);
        var retirement = RetireSessionAsync(sessionId);

        return cancellationFailure is null
            ? retirement
            : CompleteAfterCancellationFailureAsync(retirement, cancellationFailure);
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                throw new InvalidOperationException("The packet dispatcher cannot restart after shutdown.");
            }

            if (!_running)
            {
                var registrations = _registry.Freeze();
                _handlers = registrations.Where(pair => !pair.Value.IsAsync)
                    .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Bind(_resolver));
                _asyncHandlers = registrations.Where(pair => pair.Value.IsAsync)
                    .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.BindAsync(_resolver));

                if (_asyncHandlers.Count > 0)
                {
                    _asyncExecutor = new(_gameLoop, _sessions, _resolver.Resolve<IPacketSendService>())
                    {
                        Released = DispatchPending
                    };
                }

                _closedListeners = _resolver.ResolveMany<ISessionClosedListener>().ToArray();
                _everStarted = true;
                _running = true;
                _logger.Information(
                    "Packet dispatcher started with {PacketCount} registered packets and {HandlerCount} registered handlers",
                    _packets.RegisteredPackets.Count,
                    _handlers.Count + _asyncHandlers.Count
                );
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            _running = false;
            _stopped = true;

            return _stopTask ??= _asyncExecutor?.DisposeAsync().AsTask() ?? Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public bool TryDispatch(long sessionId, IPacket packet)
    {
        lock (_gate)
        {
            if (!_running)
            {
                return false;
            }

            if (!_handlers.ContainsKey(packet.GetType()) &&
                !_asyncHandlers.TryGetValue(packet.GetType(), out _))
            {
                _logger.Debug("No packet handler for {PacketType} on session {SessionId}", packet.GetType().Name, sessionId);

                return false;
            }

            if (!_sessions.TryGet(sessionId, out var session) ||
                session.NetworkSession.State == NetworkSessionState.Disconnected ||
                session.NetworkSession.Client is not { IsConnected: true })
            {
                return false;
            }

            if (_asyncExecutor?.IsBusy(sessionId) == true || _pending.ContainsKey(sessionId))
            {
                return TryHoldUntilIdle(sessionId, packet);
            }

            return DispatchCore(sessionId, packet);
        }
    }

    /// <summary>
    ///     Keeps a packet that arrived while the session's async handler runs, so it runs after it in arrival order
    ///     instead of dropping the client (it answers the server while, for example, it enters the world).
    /// </summary>
    private bool TryHoldUntilIdle(long sessionId, IPacket packet)
    {
        if (!_pending.TryGetValue(sessionId, out var queue))
        {
            queue = new();
            _pending.Add(sessionId, queue);
        }

        if (queue.Count >= MaxPendingPerSession)
        {
            _logger.Warning(
                "Session {SessionId} sent more than {Max} packets while a handler was running",
                sessionId,
                MaxPendingPerSession
            );

            return false;
        }

        queue.Enqueue(packet);
        DispatchPending(sessionId);

        return true;
    }

    private void DispatchPending(long sessionId)
    {
        lock (_gate)
        {
            while (_pending.TryGetValue(sessionId, out var queue) && _asyncExecutor?.IsBusy(sessionId) != true)
            {
                if (!_running || !_sessions.TryGet(sessionId, out _) || !queue.TryDequeue(out var packet))
                {
                    _pending.Remove(sessionId);

                    return;
                }

                if (queue.Count == 0)
                {
                    _pending.Remove(sessionId);
                }

                if (!DispatchCore(sessionId, packet))
                {
                    _logger.Warning(
                        "Dropped {PacketType} held for session {SessionId}: it could not be dispatched",
                        packet.GetType().Name,
                        sessionId
                    );
                }
            }
        }
    }

    private bool DispatchCore(long sessionId, IPacket packet)
    {
        if (!_sessions.TryGet(sessionId, out var session))
        {
            return false;
        }

        if (_asyncHandlers.TryGetValue(packet.GetType(), out var asyncHandler))
        {
            // the executor exists whenever async handlers are registered.
            var executor = _asyncExecutor!;

            if (!executor.TryReserve(session, packet, asyncHandler, out var job))
            {
                return false;
            }

            // TryReserve returned true, so job is set.
            if (_gameLoop.TryPost(new AsyncPacketDispatchWorkItem(_sessions, job!, executor)))
            {
                return true;
            }

            // TryReserve returned true, so job is set.
            executor.Release(job!, false);

            return false;
        }

        if (_gameLoop.TryPost(new PacketDispatchWorkItem(_sessions, sessionId, packet, _handlers[packet.GetType()])))
        {
            return true;
        }

        _logger.Warning(
            "Game loop inbox rejected {PacketType} for session {SessionId}: full or unavailable",
            packet.GetType().Name,
            sessionId
        );

        return false;
    }

    private Task RetireSessionAsync(long sessionId)
    {
        lock (_gate)
        {
            if (!_everStarted || _gameLoop.IsOnLoopThread || _gameLoop.Completion.IsCompleted)
            {
                var retirement = new SessionRetirementWorkItem(_sessions, sessionId, _closedListeners);
                retirement.Execute();

                return retirement.Completion;
            }

            if (_disconnects.TryGetValue(sessionId, out var pending))
            {
                return pending;
            }

            if (!_sessions.TryGet(sessionId, out _))
            {
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disconnects.Add(sessionId, completion.Task);
            _ = RetireAsync(sessionId, completion);

            return completion.Task;
        }
    }

    private static async Task CompleteAfterCancellationFailureAsync(Task retirement, Exception cancellationFailure)
    {
        try
        {
            await retirement.ConfigureAwait(false);
        }
        catch (Exception retirementFailure)
        {
            throw new AggregateException(cancellationFailure, retirementFailure);
        }

        throw new AggregateException("Async packet cancellation failed after session retirement.", cancellationFailure);
    }

    private async Task RetireAsync(long sessionId, TaskCompletionSource completion)
    {
        try
        {
            var retirement = new SessionRetirementWorkItem(_sessions, sessionId, _closedListeners);
            using var cancellation = new CancellationTokenSource();

            try
            {
                // Exactly one admission waiter per disconnect, never one per incoming packet.
                var admission = _gameLoop.PostAsync(retirement, cancellation.Token).AsTask();

                if (await Task.WhenAny(admission, _gameLoop.Completion).ConfigureAwait(false) == _gameLoop.Completion)
                {
                    await cancellation.CancelAsync().ConfigureAwait(false);
                }

                await admission.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException)
            {
                // Rejection can precede terminal completion while the loop is draining.
                await _gameLoop.Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            await Task.WhenAny(retirement.Completion, _gameLoop.Completion).ConfigureAwait(false);

            if (!retirement.Completion.IsCompleted)
            {
                // Terminal completion guarantees there is no concurrent game work to race with retirement.
                await _gameLoop.Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                retirement.Execute();
            }

            await retirement.Completion.ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            lock (_gate)
            {
                _disconnects.Remove(sessionId);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return new(StopAsync());
    }
}
