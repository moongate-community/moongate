using System.Collections.Frozen;
using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Sessions;
using Moongate.Server.Services.Packets.Internal;
using Serilog;

namespace Moongate.Server.Services.Packets;

/// <summary>Dispatches typed handlers through the existing bounded game loop inbox.</summary>
public sealed class PacketDispatchService : IPacketDispatchService
{
    private readonly Lock _gate = new();
    private readonly IGameLoopService _gameLoop;
    private readonly ISessionService _sessions;
    private readonly PacketHandlerRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly Dictionary<long, Task> _disconnects = new();
    private readonly ILogger _logger = Log.ForContext<PacketDispatchService>();

    private FrozenDictionary<Type, Action<GameSession, IPacket>> _handlers = FrozenDictionary<Type, Action<GameSession, IPacket>>.Empty;
    private bool _everStarted;
    private bool _running;
    private bool _stopped;

    public PacketDispatchService(IGameLoopService gameLoop, ISessionService sessions, PacketHandlerRegistry registry, IResolverContext resolver)
    {
        _gameLoop = gameLoop;
        _sessions = sessions;
        _registry = registry;
        _resolver = resolver;
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
                _handlers = _registry.Freeze().ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Bind(_resolver));
                _everStarted = true;
                _running = true;
                _logger.Information(
                    "Packet dispatcher started with {PacketCount} registered packets and {HandlerCount} registered handlers",
                    PacketRegistry.Default.RegisteredPackets.Count,
                    _handlers.Count
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
        }

        return Task.CompletedTask;
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

            if (!_handlers.TryGetValue(packet.GetType(), out var handler))
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

            if (_gameLoop.TryPost(new PacketDispatchWorkItem(_sessions, sessionId, packet, handler)))
            {
                return true;
            }

            _logger.Warning("Game loop inbox rejected {PacketType} for session {SessionId}: full or unavailable", packet.GetType().Name, sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public Task DisconnectAsync(long sessionId)
    {
        lock (_gate)
        {
            if (!_everStarted || _gameLoop.IsOnLoopThread || _gameLoop.Completion.IsCompleted)
            {
                var retirement = new SessionRetirementWorkItem(_sessions, sessionId);
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

    private async Task RetireAsync(long sessionId, TaskCompletionSource completion)
    {
        try
        {
            var retirement = new SessionRetirementWorkItem(_sessions, sessionId);
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
}
