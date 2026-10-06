using System.Net;
using System.Net.Sockets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Network;
using Serilog;

namespace Moongate.Server.Services.Network;

/// <summary>
///     Answers UDP pings by sending every datagram back to its sender, so a client can measure the latency of the
///     shard. It runs off the game loop and holds no game state.
/// </summary>
/// <remarks>
///     A datagram larger than <see cref="PingServerOptions.MaxDatagramSize" /> gets no answer. An endpoint that cannot
///     be bound is logged and skipped: the ping server never stops the startup.
/// </remarks>
public sealed class PingServerService : IMoongateStartupService, IDisposable
{
    private static readonly TimeSpan ErrorPause = TimeSpan.FromMilliseconds(100);

    private readonly ILogger _logger = Log.ForContext<PingServerService>();

    private readonly PingServerOptions _options;

    private readonly CancellationTokenSource _stopping = new();

    private readonly List<Socket> _sockets = [];

    private readonly List<Task> _loops = [];

    private bool _stopped;

    // Safe: sockets are bound before they are listed.
    internal IReadOnlyList<IPEndPoint> LocalEndpoints =>
        _sockets.Select(socket => (IPEndPoint)socket.LocalEndPoint!).ToArray();

    public PingServerService(PingServerOptions options)
    {
        _options = options;
    }

    public Task StartAsync()
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        foreach (var endpoint in _options.Endpoints)
        {
            var socket = new Socket(endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                socket.Bind(endpoint);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                _logger.Debug("Ping server cannot listen on {Endpoint}: {Reason}", endpoint, exception.SocketErrorCode);

                continue;
            }

            // Read here and not in the loop: a stop right after the start disposes the socket before the loop runs.
            // Safe: sockets are bound before they are listed.
            var local = (IPEndPoint)socket.LocalEndPoint!;
            _sockets.Add(socket);
            _loops.Add(Task.Run(() => EchoAsync(socket, local, _stopping.Token)));
            _logger.Debug("Ping server listening on {Endpoint} (UDP)", local);
        }

        var failed = _options.Endpoints.Count - _sockets.Count;

        if (failed > 0)
        {
            _logger.Warning(
                "Ping server cannot listen on {Failed} of {Total} UDP endpoints; is the port in use?",
                failed,
                _options.Endpoints.Count
            );
        }

        if (_sockets.Count > 0)
        {
            _logger.Information("Ping server listening on {Count} UDP endpoints", _sockets.Count);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        await _stopping.CancelAsync().ConfigureAwait(false);

        foreach (var socket in _sockets)
        {
            socket.Dispose();
        }

        await Task.WhenAll(_loops).ConfigureAwait(false);
        _sockets.Clear();
        _loops.Clear();
    }

    /// <summary>
    ///     Tells whether a datagram gets its echo: not when it is over the size limit, and not when it comes from the
    ///     ping port itself, which is another ping server's echo and would bounce between the two forever.
    /// </summary>
    internal static bool IsAnswered(int size, int senderPort, int localPort, int maxDatagramSize)
    {
        return size <= maxDatagramSize && senderPort != localPort;
    }

    /// <summary>
    ///     Tells whether the loop waits before the next receive: yes for an error that can come back at once and spin
    ///     the loop, no for one that only means this ping is lost.
    /// </summary>
    internal static bool NeedsPause(SocketError error)
    {
        return error is not (SocketError.MessageSize or SocketError.ConnectionReset);
    }

    private async Task EchoAsync(Socket socket, IPEndPoint local, CancellationToken cancellationToken)
    {
        // One byte more than the limit, so a datagram over it is seen as such instead of being cut to the limit.
        var buffer = new byte[_options.MaxDatagramSize + 1];
        var sender = new SocketAddress(socket.AddressFamily);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var received = await socket.ReceiveFromAsync(buffer, SocketFlags.None, sender, cancellationToken)
                    .ConfigureAwait(false);

                if (!IsAnswered(received, GetPort(sender), local.Port, _options.MaxDatagramSize))
                {
                    continue;
                }

                await socket.SendToAsync(buffer.AsMemory(0, received), SocketFlags.None, sender, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException exception)
            {
                // An oversized datagram on Windows or an unreachable sender: this ping is lost, the next ones are not.
                if (NeedsPause(exception.SocketErrorCode))
                {
                    await Task.Delay(ErrorPause, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Ping server stopped answering on {Endpoint}", local);

                return;
            }
        }
    }

    // The port sits in network byte order after the two bytes of the address family, for IPv4 and IPv6 alike.
    private static int GetPort(SocketAddress address)
    {
        var bytes = address.Buffer.Span;

        return (bytes[2] << 8) | bytes[3];
    }

    public void Dispose()
    {
        _stopping.Dispose();
    }
}
