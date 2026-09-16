using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Serilog;
using Moongate.Network.Data.Events;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Interfaces.Codecs;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Pipeline;

namespace Moongate.Network.Client;

/// <summary>
/// Represents a connected TCP client with async send/receive loops,
/// middleware processing and lifecycle events.
/// </summary>
public sealed class MoongateTcpClient : INetworkConnection, IAsyncDisposable, IDisposable
{
    private const int DefaultReceiveBufferSize = 8192;
    private const int DefaultMaxFrameLength = 1024 * 1024;
    private static long _sessionIdSequence;

    private readonly INetFramer? _framer;
    private readonly int _maxFrameLength;
    private readonly CancellationTokenSource _internalCancellationTokenSource = new();

    private readonly ILogger _logger = Log.ForContext<MoongateTcpClient>();
    private readonly NetMiddlewarePipeline _middlewarePipeline;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Socket _socket;
    private readonly Stream _stream;
    private int _closed;
    private ITransportCodec? _codec;

    private CancellationTokenRegistration _externalCancellationTokenRegistration;
    private byte[]? _pendingBuffer;
    private int _pendingLength;
    private Task? _receiveLoopTask;
    private int _started;

    /// <summary>
    /// Receives payload chunk size in bytes.
    /// </summary>
    public int ReceiveBufferSize { get; }

    /// <summary>
    /// Local endpoint used for this connection, when available.
    /// </summary>
    public EndPoint? LocalEndPoint
    {
        get
        {
            try
            {
                return _socket.LocalEndPoint;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }
    }


    /// <summary>
    /// Unique session identifier for this client connection.
    /// </summary>
    public long SessionId { get; }

    /// <inheritdoc />
    public INetFramer? Framer => _framer;

    /// <summary>
    /// Client remote endpoint, when connected.
    /// </summary>
    public EndPoint? RemoteEndPoint
    {
        get
        {
            try
            {
                return _socket.RemoteEndPoint;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// True when the underlying socket is connected and client not closed.
    /// </summary>
    public bool IsConnected => _socket.Connected && Volatile.Read(ref _closed) == 0;

    /// <inheritdoc />
    public Task Completion => _receiveLoopTask ?? Task.CompletedTask;

    /// <summary>
    /// Creates a client wrapper for an accepted socket.
    /// </summary>
    /// <param name="socket">Connected socket.</param>
    /// <param name="middlewares">Optional middleware list.</param>
    /// <param name="framer">
    /// Optional framer. When supplied, the receive loop accumulates middleware output and
    /// emits <see cref="OnDataReceived" /> once per complete frame instead of once per socket read.
    /// </param>
    /// <param name="receiveBufferSize">Receive chunk size in bytes.</param>
    public MoongateTcpClient(
        Socket socket,
        IEnumerable<INetMiddleware>? middlewares = null,
        INetFramer? framer = null,
        ITransportCodec? codec = null,
        int receiveBufferSize = DefaultReceiveBufferSize,
        int maxFrameLength = DefaultMaxFrameLength,
        bool noDelay = true
    ) : this(
        socket,
        new NetworkStream(socket, ownsSocket: false),
        middlewares,
        framer,
        codec,
        receiveBufferSize,
        maxFrameLength,
        noDelay
    )
    {
    }

    /// <summary>
    /// Creates a client wrapper for an accepted socket using the supplied transport stream.
    /// </summary>
    internal MoongateTcpClient(
        Socket socket,
        Stream stream,
        IEnumerable<INetMiddleware>? middlewares = null,
        INetFramer? framer = null,
        ITransportCodec? codec = null,
        int receiveBufferSize = DefaultReceiveBufferSize,
        int maxFrameLength = DefaultMaxFrameLength,
        bool noDelay = true
    )
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(stream);

        _socket = socket;
        _stream = stream;
        _middlewarePipeline = new(middlewares);
        _framer = framer;
        _codec = codec;
        socket.NoDelay = noDelay;
        ReceiveBufferSize = receiveBufferSize;
        _maxFrameLength = maxFrameLength;
        SessionId = Interlocked.Increment(ref _sessionIdSequence);
    }

    /// <summary>
    /// Closes the client connection and raises disconnect event once.
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        try
        {
            await _internalCancellationTokenSource.CancelAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Once close has started, still tear down the socket below.
        }

        ShutdownSocket();
    }

    /// <summary>
    /// Sends a payload to the connected socket.
    /// </summary>
    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (payload.IsEmpty || !IsConnected)
        {
            return;
        }

        // The whole send path runs under the lock: a stateful send middleware — the only per-connection
        // hook for a protocol that encrypts just part of a packet — must consume its state in the same
        // order the bytes reach the socket, exactly as the codec below already does.
        await _sendLock.WaitAsync(cancellationToken);

        try
        {
            var processedPayload = await _middlewarePipeline.ExecuteSendAsync(this, payload, cancellationToken);

            if (processedPayload.IsEmpty)
            {
                return;
            }

            try
            {
                var codec = Volatile.Read(ref _codec);

                if (codec is null)
                {
                    await _stream.WriteAsync(processedPayload, cancellationToken);
                }
                else
                {
                    var sendBuffer = ArrayPool<byte>.Shared.Rent(processedPayload.Length);

                    try
                    {
                        processedPayload.Span.CopyTo(sendBuffer);
                        codec.Encode(sendBuffer.AsSpan(0, processedPayload.Length));
                        await _stream.WriteAsync(sendBuffer.AsMemory(0, processedPayload.Length), cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(sendBuffer);
                    }
                }

                await _stream.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                RaiseException(ex);
                await CloseAsync(CancellationToken.None);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Adds a middleware component to this client pipeline.
    /// </summary>
    public MoongateTcpClient AddMiddleware(INetMiddleware middleware)
    {
        _middlewarePipeline.AddMiddleware(middleware);

        return this;
    }

    /// <summary>
    /// Creates an outbound client and connects to the specified endpoint.
    /// </summary>
    [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Signature follows the transport API specification.")]
    public static async Task<MoongateTcpClient> ConnectAsync(
        IPEndPoint endPoint,
        IEnumerable<INetMiddleware>? middlewares = null,
        INetFramer? framer = null,
        ITransportCodec? codec = null,
        CancellationToken cancellationToken = default,
        bool noDelay = true
    )
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(endPoint, cancellationToken);

        var client = new MoongateTcpClient(socket, middlewares, framer, codec, noDelay: noDelay);
        await client.StartAsync(cancellationToken);

        return client;
    }


    /// <summary>
    /// Checks whether this client pipeline contains at least one middleware instance of the specified type.
    /// </summary>
    public bool ContainsMiddleware<TMiddleware>()
        where TMiddleware : INetMiddleware
        => _middlewarePipeline.ContainsMiddleware<TMiddleware>();


    /// <summary>
    /// Atomically swaps the transport codec for this connection. The new codec takes effect from the next
    /// socket read; the caller must trigger the swap at a read boundary (no old-regime bytes still pending).
    /// </summary>
    /// <param name="codec">The new codec, or null to remove transport transformation.</param>
    public void SwapCodec(ITransportCodec? codec)
        => Volatile.Write(ref _codec, codec);

    /// <summary>
    /// Removes all middleware components of the specified type from this client pipeline.
    /// </summary>
    public bool RemoveMiddleware<TMiddleware>()
        where TMiddleware : INetMiddleware
        => _middlewarePipeline.RemoveMiddleware<TMiddleware>();

    /// <summary>
    /// Starts the receive loop and raises connect event.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        if (cancellationToken.CanBeCanceled)
        {
            _externalCancellationTokenRegistration =
                cancellationToken.Register(() => _ = CloseAsync(CancellationToken.None));
        }

        RaiseConnected();
        _receiveLoopTask = Task.Run(ReceiveLoopAsync, CancellationToken.None);

        return Task.CompletedTask;
    }

    private void AppendPending(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        if (_pendingBuffer is null)
        {
            _pendingBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(ReceiveBufferSize, data.Length));
        }

        var required = _pendingLength + data.Length;

        if (required > _pendingBuffer.Length)
        {
            var newCapacity = Math.Max(required, _pendingBuffer.Length * 2);
            var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
            _pendingBuffer.AsSpan(0, _pendingLength).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(_pendingBuffer);
            _pendingBuffer = newBuffer;
        }

        data.CopyTo(_pendingBuffer.AsSpan(_pendingLength));
        _pendingLength += data.Length;
    }

    /// <summary>
    /// Synchronous counterpart of <see cref="CloseAsync" />, for the blocking dispose path.
    /// </summary>
    private void CloseSync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        try
        {
            _internalCancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent disposal already tore the token source down.
        }

        ShutdownSocket();
    }

    private void ConsumePending(int count)
    {
        var remaining = _pendingLength - count;

        if (remaining > 0 && _pendingBuffer is not null)
        {
            _pendingBuffer.AsSpan(count, remaining).CopyTo(_pendingBuffer);
        }

        _pendingLength = remaining;
    }

    private void EmitFrames()
    {
        if (_framer is null || _pendingBuffer is null)
        {
            return;
        }

        while (_pendingLength > 0)
        {
            var view = _pendingBuffer.AsSpan(0, _pendingLength);

            if (!_framer.TryReadFrame(view, out var frameLength))
            {
                // Incomplete frame. If the buffer already exceeds the cap, the in-progress frame is
                // oversized (or the peer is streaming junk that never frames): reject and let the
                // receive loop close the connection before the buffer grows further.
                if (_pendingLength > _maxFrameLength)
                {
                    throw new InvalidDataException($"Incoming frame exceeds the maximum of {_maxFrameLength} bytes.");
                }

                break;
            }

            if (frameLength <= 0 || frameLength > _pendingLength)
            {
                // Malformed framer report. A framer that transforms in place has already consumed
                // keystream over these bytes, so silently dropping the buffer would leave the
                // connection open and permanently desynchronised: reject and let the receive loop
                // close it, the same way the oversize branch below does.
                throw new InvalidDataException(
                    $"Framer reported an invalid frame length of {frameLength} bytes for {_pendingLength} pending bytes."
                );
            }

            if (frameLength > _maxFrameLength)
            {
                throw new InvalidDataException(
                    $"Incoming frame of {frameLength} bytes exceeds the maximum of {_maxFrameLength} bytes."
                );
            }

            // Fresh copy so handlers can safely retain the payload.
            var frame = new byte[frameLength];
            view[..frameLength].CopyTo(frame);

            ConsumePending(frameLength);

            OnDataReceived?.Invoke(this, new(this, frame));
        }
    }

    private void RaiseConnected()
    {
        _logger.Information(
            "Client connected. SessionId={SessionId}, RemoteEndPoint={RemoteEndPoint}",
            SessionId,
            RemoteEndPoint
        );
        OnConnected?.Invoke(this, new(this));
    }

    private void RaiseDisconnected()
    {
        _logger.Information(
            "Client disconnected. SessionId={SessionId}, RemoteEndPoint={RemoteEndPoint}",
            SessionId,
            RemoteEndPoint
        );
        OnDisconnected?.Invoke(this, new(this));
    }

    private void RaiseException(Exception exception)
    {
        _logger.Error(
            exception,
            "Client exception. SessionId={SessionId}, RemoteEndPoint={RemoteEndPoint}",
            SessionId,
            RemoteEndPoint
        );
        OnException?.Invoke(this, new(exception, this));
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);

        try
        {
            while (!_internalCancellationTokenSource.IsCancellationRequested && IsConnected)
            {
                var received = await _stream.ReadAsync(
                                   buffer.AsMemory(0, ReceiveBufferSize),
                                   _internalCancellationTokenSource.Token
                               );

                if (received <= 0)
                {
                    break;
                }

                var chunk = ArrayPool<byte>.Shared.Rent(received);

                try
                {
                    buffer.AsSpan(0, received).CopyTo(chunk);

                    Volatile.Read(ref _codec)?.Decode(chunk.AsSpan(0, received));


                    var chunkMemory = new ReadOnlyMemory<byte>(chunk, 0, received);
                    var processed = await _middlewarePipeline.ExecuteAsync(
                                        this,
                                        chunkMemory,
                                        _internalCancellationTokenSource.Token
                                    );

                    if (processed.IsEmpty)
                    {
                        continue;
                    }

                    if (_framer is null)
                    {
                        // Fresh copy so the event handler can outlive the pooled chunk.
                        var payload = new byte[processed.Length];
                        processed.CopyTo(payload);
                        OnDataReceived?.Invoke(this, new(this, payload));
                    }
                    else
                    {
                        AppendPending(processed.Span);
                        EmitFrames();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(chunk);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during controlled shutdown.
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Receive loop failed for session {SessionId}", SessionId);
            RaiseException(ex);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ReleasePendingBuffer();
            await CloseAsync(CancellationToken.None);
        }
    }

    private void ReleasePendingBuffer()
    {
        if (_pendingBuffer is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_pendingBuffer);
        _pendingBuffer = null;
        _pendingLength = 0;
    }

    /// <summary>
    /// Releases the resources the receive loop relies on. Only safe once that loop has finished.
    /// </summary>
    private void ReleaseResources()
    {
        _stream.Dispose();
        _sendLock.Dispose();
        _internalCancellationTokenSource.Dispose();
        _socket.Dispose();
    }

    private void ShutdownSocket()
    {
        try
        {
            if (_socket.Connected)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // Socket might already be closed by peer.
                }
            }
        }
        finally
        {
            _socket.Close();
            _externalCancellationTokenRegistration.Dispose();
            RaiseDisconnected();
        }
    }

    /// <summary>
    /// Raised when the client is fully connected and receive loop starts.
    /// </summary>
    public event EventHandler<TcpClientEventArgs>? OnConnected;

    /// <summary>
    /// Raised when the client is disconnected.
    /// </summary>
    public event EventHandler<TcpClientEventArgs>? OnDisconnected;

    /// <summary>
    /// Raised when data is received (after middleware pipeline).
    /// </summary>
    public event EventHandler<TcpDataReceivedEventArgs>? OnDataReceived;

    /// <summary>
    /// Raised when receive/send loops throw an exception.
    /// </summary>
    public event EventHandler<TcpExceptionEventArgs>? OnException;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None);

        // Drain the receive loop before disposing the resources it relies on.
        if (_receiveLoopTask is not null)
        {
            try
            {
                await _receiveLoopTask;
            }
            catch
            {
                // Loop failures are already surfaced via OnException.
            }
        }

        await _stream.DisposeAsync();
        _sendLock.Dispose();
        _internalCancellationTokenSource.Dispose();
        _socket.Dispose();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Best-effort synchronous teardown. It must never wait on the receive loop: <c>OnDataReceived</c>
    /// handlers run on that loop, so a handler disposing its own connection would wait on itself
    /// forever. Closing the socket makes the pending read fail and the loop unwinds on its own; the
    /// resources it still uses are released once it has. Prefer <see cref="DisposeAsync" />, which
    /// drains the loop before returning.
    /// </remarks>
    public void Dispose()
    {
        CloseSync();

        var receiveLoopTask = _receiveLoopTask;

        if (receiveLoopTask is null || receiveLoopTask.IsCompleted)
        {
            ReleaseResources();

            return;
        }

        _ = receiveLoopTask.ContinueWith(
            static (_, state) => ((MoongateTcpClient)state!).ReleaseResources(),
            this,
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default
        );
    }
}
