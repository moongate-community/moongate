using System.Net;
using Moongate.Network.Framing;
using Moongate.Network.Interfaces;
using Moongate.Network.Protocol;
using Moongate.Server.Data;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Network.Data.Events;
using SquidStd.Network.Server;
using SquidStd.Network.Spans;

namespace Moongate.Server.Services.Network;

/// <summary>
/// Runs the framed TCP listener as a SquidStd lifecycle service: it starts itself, resolves
/// sessions, applies the seed handshake, and dispatches each frame to the registered
/// session-aware handler.
/// </summary>
public sealed class NetworkService : INetworkService, ISquidStdService, IAsyncDisposable
{
    private readonly ILogger _logger = Log.ForContext<NetworkService>();
    private readonly ISessionManager _sessions;
    private readonly MoongateConfig _config;
    private readonly IEnumerable<IPacketHandlerRegistration> _handlerRegistrations;
    private readonly PacketDispatch?[] _handlers = new PacketDispatch?[256];

    private SquidTcpServer? _server;

    public int Port => _server?.Port ?? 0;

    public NetworkService(
        ISessionManager sessions,
        MoongateConfig config,
        IEnumerable<IPacketHandlerRegistration> handlerRegistrations
    )
    {
        _sessions = sessions;
        _config = config;
        _handlerRegistrations = handlerRegistrations;
    }

    public void RegisterHandler<TPacket>(IPacketHandler<TPacket> handler) where TPacket : IIncomingPacket<TPacket>
    {
        ArgumentNullException.ThrowIfNull(handler);

        var opCode = TPacket.PacketId;

        if (_handlers[opCode] is not null)
        {
            throw new InvalidOperationException($"A handler for packet 0x{opCode:X2} is already registered.");
        }

        _handlers[opCode] = (session, packet) =>
        {
            var reader = new SpanReader(packet);
            var decoded = TPacket.Read(ref reader);

            handler.Handle(decoded, new PacketContext(session));
        };
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var registration in _handlerRegistrations)
        {
            registration.Register(this);
        }

        var endpoint = new IPEndPoint(IPAddress.Parse(_config.Network.Address), _config.Network.Port);
        _server = new SquidTcpServer(endpoint, new UoPacketFramer());

        _server.OnClientConnect += OnClientConnect;
        _server.OnClientDisconnect += OnClientDisconnect;
        _server.OnDataReceived += OnDataReceived;
        _server.OnException += OnException;

        await _server.StartAsync(cancellationToken);
        _logger.Information("Network service listening on {Address}:{Port}", _config.Network.Address, Port);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_server is null)
        {
            return;
        }

        await _server.StopAsync(cancellationToken);
        await _server.DisposeAsync();
        _server = null;
    }

    private void OnClientConnect(object? sender, SquidStdTcpClientEventArgs e)
    {
        _sessions.GetOrCreate(e.Client);
        _logger.Information("Client connected: {SessionId}", e.Client.SessionId);
    }

    private void OnClientDisconnect(object? sender, SquidStdTcpClientEventArgs e)
    {
        _sessions.Remove(e.Client.SessionId);
        _logger.Information("Client disconnected: {SessionId}", e.Client.SessionId);
    }

    private void OnDataReceived(object? sender, SquidStdTcpDataReceivedEventArgs e)
    {
        var session = _sessions.GetOrCreate(e.Client);
        var frame = e.Data.Span;

        var handshake = SeedHandshake.Process(session, frame, out var consumed);

        if (handshake == SeedHandshakeResult.Reject)
        {
            _logger.Warning("Rejecting session {SessionId}: malformed seed handshake.", session.SessionId);
            _ = e.Client.CloseAsync();

            return;
        }

        if (handshake == SeedHandshakeResult.Consumed)
        {
            frame = frame[consumed..];

            if (frame.IsEmpty)
            {
                return;
            }
        }

        Dispatch(session, frame);
    }

    private void Dispatch(PlayerSession session, ReadOnlySpan<byte> frame)
    {
        var opCode = frame[0];
        var handler = _handlers[opCode];

        if (handler is null)
        {
            var info = PacketsInfo.GetPacket(opCode);
            _logger.Warning(
                "No handler for packet 0x{OpCode:X2} ({Name}) from session {SessionId} - not implemented yet",
                opCode,
                info?.Name ?? "Unknown",
                session.SessionId
            );

            return;
        }

        try
        {
            handler(session, frame);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Handler for 0x{OpCode:X2} threw for session {SessionId}", opCode, session.SessionId);
        }
    }

    private void OnException(object? sender, SquidStdTcpExceptionEventArgs e)
    {
        _logger.Error(e.Exception, "Network exception");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
