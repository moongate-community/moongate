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
    private readonly ILogger _logger = Log.ForContext<PingServerService>();

    private readonly PingServerOptions _options;

    private readonly CancellationTokenSource _stopping = new();

    private readonly List<Socket> _sockets = [];

    private readonly List<Task> _loops = [];

    internal IReadOnlyList<IPEndPoint> LocalEndpoints => _sockets.Select(socket => (IPEndPoint)socket.LocalEndPoint!).ToArray();

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
                _logger.Warning(
                    "Ping server cannot listen on {Endpoint}: {Reason}",
                    endpoint,
                    exception.SocketErrorCode
                );

                continue;
            }

            _sockets.Add(socket);
            _loops.Add(Task.Run(() => EchoAsync(socket, _stopping.Token)));
            _logger.Information("Ping server listening on {Endpoint} (UDP)", socket.LocalEndPoint);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);

        foreach (var socket in _sockets)
        {
            socket.Dispose();
        }

        await Task.WhenAll(_loops).ConfigureAwait(false);
        _sockets.Clear();
        _loops.Clear();
    }

    private async Task EchoAsync(Socket socket, CancellationToken cancellationToken)
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

                if (received > _options.MaxDatagramSize)
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
            catch (SocketException)
            {
                // An oversized datagram on Windows or an unreachable sender: this ping is lost, the next ones are not.
            }
        }
    }

    public void Dispose()
    {
        _stopping.Dispose();
    }
}
