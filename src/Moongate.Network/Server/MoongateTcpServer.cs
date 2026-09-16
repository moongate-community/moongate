using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Serilog;
using Moongate.Network.Client;
using Moongate.Network.Data;
using Moongate.Network.Data.Events;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Interfaces.Server;

namespace Moongate.Network.Server;

/// <summary>
/// High-throughput TCP server with client lifecycle events and middleware-enabled payload dispatch.
/// Supports Start/Stop/Start cycles by recreating the underlying socket on each Start.
/// </summary>
public sealed class MoongateTcpServer : INetworkServer, IAsyncDisposable, IDisposable
{
    private const int DefaultBacklog = 512;
    private const int AcceptRetryDelayMilliseconds = 50;
    private readonly ConcurrentDictionary<long, MoongateTcpClient> _clients = new();
    private readonly Func<ConnectionPipeline>? _connectionPipelineFactory;
    private readonly IPEndPoint _endPoint;
    private readonly INetFramer? _framer;

    private readonly ILogger _logger = Log.ForContext<MoongateTcpServer>();
    private readonly Lock _middlewareSync = new();
    private readonly int _receiveBufferSize;
    private readonly int _maxFrameLength;
    private readonly bool _noDelay;
    private Task? _acceptLoopTask;
    private CancellationTokenSource? _listenerCancellationTokenSource;

    private INetMiddleware[] _middlewares = [];
    private Socket? _serverSocket;
    private int _started;


    /// <summary>
    /// Current listening port. Returns 0 when the server is stopped.
    /// </summary>
    public int Port => ((IPEndPoint?)_serverSocket?.LocalEndPoint)?.Port ?? 0;

    /// <summary>
    /// True when the server is currently accepting connections.
    /// </summary>
    public bool IsRunning => Volatile.Read(ref _started) != 0;

    /// <summary>
    /// Initializes a TCP server bound to the given endpoint.
    /// </summary>
    /// <param name="endPoint">Endpoint to bind on every <c>StartAsync</c>.</param>
    /// <param name="framer">
    /// Optional framer template. The same instance is shared by all accepted clients,
    /// so implementations must be stateless or thread-safe.
    /// </param>
    /// <param name="receiveBufferSize">Per-client receive chunk size.</param>
    /// <param name="connectionPipelineFactory">
    /// Optional factory invoked once per accepted connection to produce its transport configuration.
    /// It MUST return fresh per-connection state — in particular a new <c>ITransportCodec</c> instance per
    /// call — because codecs are stateful and must not be shared across connections.
    /// </param>
    public MoongateTcpServer(
        IPEndPoint endPoint,
        INetFramer? framer = null,
        int receiveBufferSize = 8192,
        Func<ConnectionPipeline>? connectionPipelineFactory = null,
        int maxFrameLength = 1024 * 1024,
        bool noDelay = true
    )
    {
        _endPoint = endPoint;
        _framer = framer;
        _receiveBufferSize = receiveBufferSize;
        _connectionPipelineFactory = connectionPipelineFactory;
        _maxFrameLength = maxFrameLength;
        _noDelay = noDelay;
    }

    /// <summary>
    /// Starts accepting clients. Recreates the listening socket on every call,
    /// so Stop/Start cycles are supported.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        _serverSocket = new(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _serverSocket.Bind(_endPoint);
        _serverSocket.Listen(DefaultBacklog);

        _listenerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = Task.Run(AcceptLoopAsync, CancellationToken.None);

        _logger.Information("TCP server listening on {LocalEndPoint}", _serverSocket.LocalEndPoint);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops accepting new clients and closes all active clients.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        if (_listenerCancellationTokenSource is not null)
        {
            await _listenerCancellationTokenSource.CancelAsync();
        }

        var socket = _serverSocket;

        try
        {
            socket?.Close();
        }
        catch (SocketException)
        {
            // Listener may already be closed.
        }

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Stop was cancelled by caller; clients are still cleaned up below.
            }
        }

        var clients = _clients.Values.ToArray();

        for (var i = 0; i < clients.Length; i++)
        {
            await clients[i].DisposeAsync();
        }

        _clients.Clear();

        socket?.Dispose();
        _serverSocket = null;

        _listenerCancellationTokenSource?.Dispose();
        _listenerCancellationTokenSource = null;
        _acceptLoopTask = null;
    }

    /// <summary>
    /// Registers middleware in execution order.
    /// </summary>
    public MoongateTcpServer AddMiddleware(INetMiddleware middleware)
    {
        lock (_middlewareSync)
        {
            _middlewares = [.. _middlewares, middleware];
        }

        return this;
    }

    private async Task AcceptLoopAsync()
    {
        var cts = _listenerCancellationTokenSource;
        var serverSocket = _serverSocket;

        if (cts is null || serverSocket is null)
        {
            return;
        }

        while (!cts.IsCancellationRequested)
        {
            // Held outside the try so every catch below can release a connection that was accepted
            // but never started. A socket accepted moments before shutdown is still a socket, so the
            // two catches that break release it too.
            Socket? clientSocket = null;
            Stream? clientStream = null;
            MoongateTcpClient? client = null;

            try
            {
                clientSocket = await serverSocket.AcceptAsync(cts.Token);
                clientStream = new NetworkStream(clientSocket, ownsSocket: false);

                var pipeline = _connectionPipelineFactory?.Invoke();
                var middlewares = pipeline?.Middlewares ?? _middlewares;
                var framer = pipeline?.Framer ?? _framer;
                var codec = pipeline?.Codec;
                client = new MoongateTcpClient(
                    clientSocket,
                    clientStream,
                    middlewares: middlewares,
                    framer: framer,
                    codec: codec,
                    receiveBufferSize: _receiveBufferSize,
                    maxFrameLength: _maxFrameLength,
                    noDelay: _noDelay
                );
                WireClientEvents(client);

                _clients[client.SessionId] = client;
                await client.StartAsync(cts.Token);

                // Handover complete. The started client owns the socket and the stream and releases
                // both when it disconnects, so the loop must not touch either again — clearing the
                // locals is what keeps the catches below off a live connection.
                client = null;
                clientStream = null;
                clientSocket = null;
            }
            catch (OperationCanceledException)
            {
                await ReleaseUnstartedConnectionAsync(client, clientStream, clientSocket);

                break;
            }
            catch (ObjectDisposedException)
            {
                await ReleaseUnstartedConnectionAsync(client, clientStream, clientSocket);

                break;
            }
            catch (Exception ex)
            {
                await ReleaseUnstartedConnectionAsync(client, clientStream, clientSocket);

                _logger.Error(ex, "Accept loop failed");
                OnException?.Invoke(this, new(ex));

                // Only a failing accept can spin: file descriptor exhaustion, typically, where every
                // AcceptAsync throws at once, so retrying immediately would burn a core and flood the
                // log. Back off briefly there; the loop condition above picks up shutdown afterwards.
                // The delay is deliberately untokened: the token source may already be disposed here,
                // and an exception thrown from a catch block would escape the loop entirely.
                // Everything after the accept — for example, a connection pipeline factory that throws —
                // needs a fresh inbound connection to fail at all, so it cannot spin, and
                // delaying it would only cap new-connection throughput at one per interval.
                if (ex is SocketException)
                {
                    await Task.Delay(AcceptRetryDelayMilliseconds);
                }
            }
        }
    }

    /// <summary>
    /// Releases a connection that was accepted but never handed over to a started client. Before the
    /// client exists the loop owns the raw socket and its stream and must close them itself, or the
    /// peer is left hanging in an ESTABLISHED connection nobody serves and the descriptor leaks; once
    /// the client exists it owns both, so only the client is disposed. Never throws: it runs from a
    /// catch block, where an escaping exception would tear the accept loop down for good.
    /// </summary>
    private async Task ReleaseUnstartedConnectionAsync(
        MoongateTcpClient? client,
        Stream? clientStream,
        Socket? clientSocket
    )
    {
        try
        {
            if (client is not null)
            {
                // The registration happens before StartAsync, so a failure there would otherwise
                // leave a zombie entry for a client that never ran.
                _clients.TryRemove(client.SessionId, out var _);
                await client.DisposeAsync();

                return;
            }

            if (clientStream is not null)
            {
                await clientStream.DisposeAsync();
            }

            clientSocket?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to release an accepted connection");
        }
    }


    private void WireClientEvents(MoongateTcpClient client)
    {
        client.OnConnected += (_, args) =>
                              {
                                  _logger.Debug(
                                      "OnClientConnect. SessionId={SessionId}, RemoteEndPoint={RemoteEndPoint}",
                                      args.Client.SessionId,
                                      args.Client.RemoteEndPoint
                                  );
                                  OnClientConnect?.Invoke(this, args);
                              };
        client.OnDataReceived += (_, args) =>
                                 {
                                     _logger.Verbose(
                                         "OnDataReceived. SessionId={SessionId}, Bytes={Bytes}",
                                         args.Client.SessionId,
                                         args.Data.Length
                                     );
                                     OnDataReceived?.Invoke(this, args);
                                 };
        client.OnException += (_, args) =>
                              {
                                  _logger.Error(
                                      args.Exception,
                                      "OnException. SessionId={SessionId}",
                                      args.Client?.SessionId
                                  );
                                  OnException?.Invoke(this, args);
                              };
        client.OnDisconnected += (_, args) =>
                                 {
                                     _clients.TryRemove(args.Client.SessionId, out var _);
                                     _logger.Debug(
                                         "OnClientDisconnect. SessionId={SessionId}, RemoteEndPoint={RemoteEndPoint}",
                                         args.Client.SessionId,
                                         args.Client.RemoteEndPoint
                                     );
                                     OnClientDisconnect?.Invoke(this, args);
                                 };
    }

    /// <summary>
    /// Raised when a client connects.
    /// </summary>
    public event EventHandler<TcpClientEventArgs>? OnClientConnect;

    /// <summary>
    /// Raised when a client disconnects.
    /// </summary>
    public event EventHandler<TcpClientEventArgs>? OnClientDisconnect;

    /// <summary>
    /// Raised when a client sends data after middleware processing.
    /// </summary>
    public event EventHandler<TcpDataReceivedEventArgs>? OnDataReceived;

    /// <summary>
    /// Raised when an exception happens in accept loop or client loops.
    /// </summary>
    public event EventHandler<TcpExceptionEventArgs>? OnException;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await StopAsync(CancellationToken.None);

    /// <inheritdoc />
    /// <remarks>
    /// Best-effort synchronous teardown. It must never wait on the accept loop or on a client receive
    /// loop: a server <c>OnDataReceived</c> handler runs on a client's receive loop, and the async
    /// path drains that loop, so a handler disposing its own server would wait on itself forever.
    /// Closing the listener and every client socket unwinds both loops. Prefer
    /// <see cref="DisposeAsync" />, which drains them before returning.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _listenerCancellationTokenSource?.Cancel();

        var socket = _serverSocket;

        try
        {
            socket?.Close();
        }
        catch (SocketException)
        {
            // Listener may already be closed.
        }

        var clients = _clients.Values.ToArray();

        for (var i = 0; i < clients.Length; i++)
        {
            clients[i].Dispose();
        }

        _clients.Clear();

        socket?.Dispose();
        _serverSocket = null;

        // The token source is cancelled but deliberately left undisposed: the accept loop still holds
        // it and may be mid-iteration with a socket already accepted, and reading Token from a
        // disposed source there would abandon that socket. Clearing the field below also puts it out
        // of reach of any later StopAsync, so this path knowingly leaks the one registration the
        // linked source holds on the caller's parent token, once per start/dispose cycle. That is the
        // price of not stranding an accepted socket, and disposal is left to DisposeAsync, which
        // drains the loop first and so can release the source safely. Prefer it wherever the caller
        // can await.
        _listenerCancellationTokenSource = null;
        _acceptLoopTask = null;
    }
}
