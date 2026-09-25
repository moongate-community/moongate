using Moongate.Network.Packets.Registry;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Network.Events;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services.Login;

/// <summary>
///     Connects the login transport to login-owned sessions and ordered packet dispatch.
/// </summary>
public sealed class LoginServerService : IMoongateStartupService
{
    private readonly INetworkService _network;
    private readonly IConnectionService _connections;
    private readonly ILoginSessionService _sessions;
    private readonly LoginPacketDispatchService _dispatcher;
    private readonly IPacketSendService _sender;
    private readonly ILogger _logger = Log.ForContext<LoginServerService>();
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly Lock _gate = new();
    private readonly Lock _cleanupGate = new();
    private readonly HashSet<Task> _cleanups = new();
    private bool _stopping;

    public LoginServerService(
        INetworkService network,
        IConnectionService connections,
        ILoginSessionService sessions,
        LoginPacketDispatchService dispatcher,
        IPacketSendService sender
    )
    {
        _network = network;
        _connections = connections;
        _sessions = sessions;
        _dispatcher = dispatcher;
        _sender = sender;
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_stopping)
            {
                throw new InvalidOperationException("Login server cannot restart after shutdown.");
            }

            return _lifecycle.StartAsync(StartCoreAsync);
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
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

    private void OnAccepted(object? sender, NetworkConnectionEventArgs args)
    {
        _sessions.GetOrCreate(args.Connection);
        _logger.Information(
            "Login client connected from {Address} with session {SessionId}",
            args.Connection.RemoteEndPoint,
            args.Connection.SessionId
        );
    }

    private void OnClosed(object? sender, NetworkConnectionEventArgs args)
    {
        if (!_sessions.TryGet(args.Connection.SessionId, out var session) ||
            !ReferenceEquals(session.NetworkSession.Client, args.Connection) ||
            !_sessions.Remove(session))
        {
            return;
        }

        TrackCleanup(
            Task.WhenAll(
                _dispatcher.DisconnectAsync(session),
                _sender.DisconnectAsync(args.Connection.SessionId, args.Connection)
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

        if (PacketRegistry.Default.TryDecode(args.Data.Span, out var packet, out var opCode) &&
            _dispatcher.TryDispatch(args.Connection.SessionId, packet))
        {
            return;
        }

        _logger.Warning(
            "Rejected login packet from session {SessionId}, opcode 0x{OpCode:X2}",
            args.Connection.SessionId,
            opCode
        );
        TrackCleanup(_connections.DisconnectAsync(args.Connection.SessionId, args.Connection));
    }

    private async Task StartCoreAsync()
    {
        _network.ConnectionAccepted += OnAccepted;
        _network.ConnectionClosed += OnClosed;
        _network.DataReceived += OnData;

        try
        {
            await _network.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            await StopCoreAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        try
        {
            await _network.StopAsync().ConfigureAwait(false);
            Task[] pending;

            lock (_cleanupGate)
            {
                pending = _cleanups.ToArray();
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        finally
        {
            _network.ConnectionAccepted -= OnAccepted;
            _network.ConnectionClosed -= OnClosed;
            _network.DataReceived -= OnData;
        }
    }

    private void TrackCleanup(Task cleanup)
    {
        lock (_cleanupGate)
        {
            _cleanups.Add(cleanup);
        }

        _ = ObserveCleanupAsync(cleanup);
    }

    private async Task ObserveCleanupAsync(Task cleanup)
    {
        try
        {
            await cleanup.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Login connection cleanup failed");
        }
        finally
        {
            lock (_cleanupGate)
            {
                _cleanups.Remove(cleanup);
            }
        }
    }
}
