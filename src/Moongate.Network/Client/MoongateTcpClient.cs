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
using Moongate.Network.Types.Client;

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
    private readonly Lock _lifecycleLock = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _sendsDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TcpClientState _state;
    private int _admittedSends;
    private ITransportCodec? _codec;

    private CancellationTokenRegistration _externalCancellationTokenRegistration;
    private byte[]? _pendingBuffer;
    private int _pendingLength;
    private Task? _receiveLoopTask;

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
    public bool IsConnected
    {
        get
        {
            lock (_lifecycleLock)
            {
                return (_state is TcpClientState.Created or TcpClientState.Running) && _socket.Connected;
            }
        }
    }

    /// <inheritdoc />
    public Task Completion => _completion.Task;

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
    /// Requests connection closure without waiting for callbacks or resource cleanup.
    /// </summary>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        RequestClose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends bytes, preserving transformation order on the wire. Keep the payload immutable until completion.
    /// </summary>
    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        lock (_lifecycleLock)
        {
            if (_state is not (TcpClientState.Created or TcpClientState.Running) || !_socket.Connected)
            {
                throw new IOException("The connection is closed.");
            }

            if (payload.IsEmpty)
            {
                return;
            }

            _admittedSends++;
        }

        try
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _internalCancellationTokenSource.Token);
            var sendToken = linkedCancellation.Token;
            await _sendLock.WaitAsync(sendToken).ConfigureAwait(false);
            try
            {
                // Cancellation before any stateful transform does not compromise the stream.
                sendToken.ThrowIfCancellationRequested();
                lock (_lifecycleLock)
                {
                    // A prior sender may have closed the connection before deferred cleanup cancels I/O.
                    if (_state is not (TcpClientState.Created or TcpClientState.Running))
                    {
                        throw new IOException("The connection is closed.");
                    }
                }
                try
                {
                    var processed = await _middlewarePipeline.ExecuteSendAsync(this, payload, sendToken)
                        .ConfigureAwait(false);
                    if (processed.IsEmpty)
                    {
                        return;
                    }

                    var codec = Volatile.Read(ref _codec);
                    if (codec is null)
                    {
                        await _stream.WriteAsync(processed, sendToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var rented = ArrayPool<byte>.Shared.Rent(processed.Length);
                        try
                        {
                            processed.Span.CopyTo(rented);
                            codec.Encode(rented.AsSpan(0, processed.Length));
                            await _stream.WriteAsync(rented.AsMemory(0, processed.Length), sendToken)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(rented);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (!IsExpectedShutdown(exception))
                    {
                        RaiseExceptionSafely(exception);
                    }
                    RequestClose();
                    throw;
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
        finally
        {
            lock (_lifecycleLock)
            {
                _admittedSends--;
                if (_admittedSends == 0 && _state is not (TcpClientState.Created or TcpClientState.Running))
                {
                    _sendsDrained.TrySetResult();
                }
            }
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
        MoongateTcpClient? client = null;
        try
        {
            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
            client = new MoongateTcpClient(socket, middlewares, framer, codec, noDelay: noDelay);
            await client.StartAsync(cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            if (client is null)
            {
                socket.Dispose();
            }
            else
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
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
        TaskCompletionSource receiveFinished;
        lock (_lifecycleLock)
        {
            if (_state == TcpClientState.Running)
            {
                return Task.CompletedTask;
            }
            if (_state != TcpClientState.Created)
            {
                return Task.FromException(new InvalidOperationException("A closed connection cannot be restarted."));
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            _state = TcpClientState.Running;
            receiveFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            // Publish before registration and callbacks: either can synchronously request close.
            _receiveLoopTask = receiveFinished.Task;
        }

        try
        {
            _externalCancellationTokenRegistration = cancellationToken.Register(RequestClose);
            if (IsConnected)
            {
                RaiseConnected();
            }
            _ = Task.Run(() => RunReceiveAsync(receiveFinished), CancellationToken.None);
        }
        catch (Exception exception)
        {
            RaiseExceptionSafely(exception);
            RequestClose();
            receiveFinished.TrySetResult();
        }
        return Task.CompletedTask;
    }

    private async Task RunReceiveAsync(TaskCompletionSource receiveFinished)
    {
        try
        {
            await ReceiveLoopAsync().ConfigureAwait(false);
        }
        finally
        {
            receiveFinished.TrySetResult();
        }
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
        if (OnDisconnected is not { } subscribers)
        {
            return;
        }
        foreach (EventHandler<TcpClientEventArgs> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, new(this));
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Disconnect subscriber failed for session {SessionId}", SessionId);
            }
        }
    }

    private void RaiseExceptionSafely(Exception exception)
    {
        _logger.Error(exception, "Client exception for session {SessionId}", SessionId);
        if (OnException is not { } subscribers)
        {
            return;
        }
        foreach (EventHandler<TcpExceptionEventArgs> subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, new(exception, this));
            }
            catch (Exception subscriberException)
            {
                _logger.Error(subscriberException, "Diagnostic subscriber failed for session {SessionId}", SessionId);
            }
        }
    }

    private bool IsExpectedShutdown(Exception exception)
    {
        lock (_lifecycleLock)
        {
            return _state is not (TcpClientState.Created or TcpClientState.Running)
                && exception is OperationCanceledException or ObjectDisposedException or IOException or SocketException;
        }
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
        catch (Exception exception)
        {
            if (!IsExpectedShutdown(exception))
            {
                RaiseExceptionSafely(exception);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ReleasePendingBuffer();
            RequestClose();
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

    private void RequestClose()
    {
        Task receiveTask;
        lock (_lifecycleLock)
        {
            if (_state is not (TcpClientState.Created or TcpClientState.Running))
            {
                return;
            }
            _state = TcpClientState.Closing;
            receiveTask = _receiveLoopTask ?? Task.CompletedTask;
            if (_admittedSends == 0)
            {
                _sendsDrained.TrySetResult();
            }
        }

        // Never invoke token callbacks or release resources on a synchronous application callback.
        _ = Task.Run(() => CleanupAsync(receiveTask));
    }

    private async Task CleanupAsync(Task receiveTask)
    {
        var failures = new List<Exception>();
        Task cancellationTask;
        try
        {
            cancellationTask = _internalCancellationTokenSource.CancelAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
            cancellationTask = Task.CompletedTask;
        }

        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
            // The peer may have already disconnected.
        }
        catch (ObjectDisposedException)
        {
            // Socket ownership may have been released externally.
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        AttemptRelease(_socket.Close, failures);
        await AttemptAsync(cancellationTask, failures).ConfigureAwait(false);
        await AttemptAsync(receiveTask, failures).ConfigureAwait(false);
        await _sendsDrained.Task.ConfigureAwait(false);
        lock (_lifecycleLock)
        {
            _state = TcpClientState.Closed;
        }
        RaiseDisconnected();

        try
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        AttemptRelease(_sendLock.Dispose, failures);
        AttemptRelease(_externalCancellationTokenRegistration.Dispose, failures);
        AttemptRelease(_internalCancellationTokenSource.Dispose, failures);
        AttemptRelease(_socket.Dispose, failures);
        lock (_lifecycleLock)
        {
            _state = TcpClientState.Disposed;
        }
        if (failures.Count == 0)
        {
            _completion.TrySetResult();
        }
        else
        {
            // Observe the fault even for synchronous Dispose; callers can still await the same task.
            _completion.TrySetException(failures.Count == 1 ? failures[0] : new AggregateException(failures));
            _ = _completion.Task.Exception;
        }
    }

    private static async Task AttemptAsync(Task task, List<Exception> failures)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void AttemptRelease(Action release, List<Exception> failures)
    {
        try
        {
            release();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    /// <summary>
    /// Raised when the client is fully connected and receive loop starts.
    /// </summary>
    public event EventHandler<TcpClientEventArgs>? OnConnected;

    /// <summary>
    /// Raised once when I/O has drained. Resource cleanup may still be running; await Completion outside callbacks.
    /// </summary>
    public event EventHandler<TcpClientEventArgs>? OnDisconnected;

    /// <summary>
    /// Raised synchronously with a stable payload copy after middleware and optional framing.
    /// Callback failures close this connection; do not use async-void handlers.
    /// </summary>
    public event EventHandler<TcpDataReceivedEventArgs>? OnDataReceived;

    /// <summary>
    /// Raised when receive/send loops throw an exception.
    /// </summary>
    public event EventHandler<TcpExceptionEventArgs>? OnException;

    /// <inheritdoc />
    /// <remarks>Do not synchronously wait for this task from a connection callback.</remarks>
    public ValueTask DisposeAsync()
    {
        RequestClose();
        return new ValueTask(Completion);
    }

    /// <inheritdoc />
    /// <remarks>Requests cleanup without waiting, so it is safe inside synchronous callbacks.</remarks>
    public void Dispose()
    {
        RequestClose();
    }
}
