using System.Buffers.Binary;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Network.Events;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services.Game;

/// <summary>
///     Bridges synchronous transport notifications to owned game work and session retirement.
/// </summary>
public sealed class GameServerService : IGameServerService
{
    private readonly PacketRegistry _packets;
    private readonly INetworkService _network;
    private readonly IConnectionService _connections;
    private readonly ISessionService _sessions;
    private readonly IPacketDispatchService _dispatcher;
    private readonly IPacketSendService _sender;
    private readonly ILogger _logger = Log.ForContext<GameServerService>();
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly Lock _lifecycleGate = new();
    private readonly Lock _cleanupGate = new();
    private readonly HashSet<Task> _cleanups = [];
    private readonly List<Exception> _failures = [];
    private bool _stopping;

    public GameServerService(
        INetworkService network,
        IConnectionService connections,
        ISessionService sessions,
        IPacketDispatchService dispatcher,
        IPacketSendService sender,
        PacketRegistry? packets = null
    )
    {
        _packets = packets ?? PacketRegistry.Default;
        _network = network;
        _connections = connections;
        _sessions = sessions;
        _dispatcher = dispatcher;
        _sender = sender;
    }

    public Task StartAsync()
    {
        lock (_lifecycleGate)
        {
            if (_stopping)
            {
                throw new InvalidOperationException("The game server cannot start after shutdown begins.");
            }

            return _lifecycle.StartAsync(StartCoreAsync);
        }
    }

    public Task StopAsync()
    {
        lock (_lifecycleGate)
        {
            _stopping = true;

            return _lifecycle.StopAsync(async startup =>
                {
                    if (startup is not null)
                    {
                        await startup.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    }

                    await StopCoreAsync().ConfigureAwait(false);
                }
            );
        }
    }

    private static async Task CaptureCleanup(Func<Task> cleanup)
    {
        await cleanup().ConfigureAwait(false);
    }

    private void OnAccepted(object? sender, NetworkConnectionEventArgs args)
    {
        _sessions.GetOrCreate(args.Connection);
        _logger.Information(
            "Client connected from {Address} with session ID {SessionId}",
            args.Connection.RemoteEndPoint,
            args.Connection.SessionId
        );
    }

    private void OnClosed(object? sender, NetworkConnectionEventArgs args)
    {
        TrackCleanup(() => Task.WhenAll(
                CaptureCleanup(() => _sender.DisconnectAsync(args.Connection.SessionId)),
                CaptureCleanup(() => _dispatcher.DisconnectAsync(args.Connection.SessionId))
            )
        );
    }

    private void OnData(object? sender, NetworkDataEventArgs args)
    {
        if (!_sessions.TryGet(args.Connection.SessionId, out var session) ||
            !ReferenceEquals(session.NetworkSession.Client, args.Connection))
        {
            return;
        }

        if (session.NetworkSession.Seed is null && args.Data.Length == sizeof(uint))
        {
            var seed = BinaryPrimitives.ReadUInt32BigEndian(args.Data.Span);

            if (seed == 0)
            {
                TrackCleanup(() => _connections.DisconnectAsync(args.Connection.SessionId, args.Connection));

                return;
            }

            session.NetworkSession.SetSeed(seed);

            return;
        }

        // Decode now: transport memory is borrowed only until this callback returns.
        if (_packets.TryDecode(args.Data.Span, out var packet, out var opCode) &&
            _dispatcher.TryDispatch(args.Connection.SessionId, packet))
        {
            return;
        }

        var packetName = _packets.TryGetDescriptor(opCode, out var descriptor)
            ? descriptor.PacketType.Name
            : "Unknown";
        _logger.Warning(
            "Rejected packet from session {SessionId}, opcode {OpCode}, name {PacketName}",
            args.Connection.SessionId,
            opCode,
            packetName
        );
        TrackCleanup(() => _connections.DisconnectAsync(args.Connection.SessionId));
    }

    private async Task RunCleanupAsync(Func<Task> cleanup, TaskCompletionSource completion)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (_cleanupGate)
            {
                _failures.Add(exception);
            }

            _logger.Error(exception, "Game connection cleanup failed");
        }
        finally
        {
            lock (_cleanupGate)
            {
                completion.TrySetResult();
                _cleanups.Remove(completion.Task);
            }
        }
    }

    private async Task StartCoreAsync()
    {
        _network.ConnectionAccepted += OnAccepted;
        _network.DataReceived += OnData;
        _network.ConnectionClosed += OnClosed;

        try
        {
            _logger.Information("Starting game server packet coordination");
            await _network.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            lock (_lifecycleGate)
            {
                _stopping = true;
            }

            try
            {
                await StopCoreAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Game cleanup failed after startup failure");
            }

            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        List<Exception> failures = [];

        try
        {
            try
            {
                await _network.StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            Task[] pending;

            lock (_cleanupGate)
            {
                pending = _cleanups.ToArray();
            }

            await Task.WhenAll(pending).ConfigureAwait(false);

            lock (_cleanupGate)
            {
                failures.AddRange(_failures);
            }
        }
        finally
        {
            _network.ConnectionAccepted -= OnAccepted;
            _network.DataReceived -= OnData;
            _network.ConnectionClosed -= OnClosed;
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    private void TrackCleanup(Func<Task> cleanup)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_cleanupGate)
        {
            _cleanups.Add(completion.Task);
        }

        _ = RunCleanupAsync(cleanup, completion);
    }
}
