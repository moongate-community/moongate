using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Data;
using Moongate.Network.Data.Config;
using Moongate.Network.Data.Events;
using Moongate.Network.Interfaces.Framing;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Interfaces.Server;
using Moongate.Network.Server.Internal;
using Moongate.Network.Types.Server;
using Serilog;

namespace Moongate.Network.Server;

/// <summary>
/// TCP listener with serialized, restartable generations and owned connection cleanup.
/// </summary>
public sealed class MoongateTcpServer : INetworkServer, IAsyncDisposable, IDisposable
{
    private const int DefaultBacklog = 512;
    private const int AcceptRetryDelayMilliseconds = 50;
    private readonly Lock _lifecycleSync = new();
    private readonly Lock _middlewareSync = new();
    private readonly Dictionary<long, MoongateTcpClient> _clients = new();
    private readonly Dictionary<long, TaskCompletionSource> _clientStarts = new();
    private readonly Dictionary<long, Task> _clientCleanups = new();
    private readonly HashSet<AcceptedConnectionSetup> _setups = [];
    private readonly List<Exception> _cleanupErrors = [];
    private readonly Func<ConnectionPipeline>? _connectionPipelineFactory;
    private readonly IPEndPoint _endPoint;
    private readonly INetFramer? _framer;
    private readonly ILogger _logger = Log.ForContext<MoongateTcpServer>();
    private readonly int _receiveBufferSize;
    private readonly int _maxFrameLength;
    private readonly bool _noDelay;
    private readonly TcpServerOptions? _configuredOptions;
    private int _admittedConnections;
    private int _preparingConnections;
    private INetMiddleware[] _middlewares = [];
    private TcpServerState _state;
    private bool _disposeRequested;
    private int _port;
    private IPEndPoint? _boundEndPoint;
    private Socket? _serverSocket;
    private CancellationTokenSource? _listenerCancellationTokenSource;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task _startTask = Task.CompletedTask;
    private Task _stopTask = Task.CompletedTask;
    private Task _acceptLoopTask = Task.CompletedTask;
    private Task? _admissionStopTask;

    /// <summary>Raised when an accepted client connects.</summary>
    public event EventHandler<TcpClientEventArgs>? OnClientConnect;

    /// <summary>Raised when a connection closes; resource cleanup may still be in progress.</summary>
    public event EventHandler<TcpClientEventArgs>? OnClientDisconnect;

    /// <summary>Raised synchronously for each received chunk or frame.</summary>
    public event EventHandler<TcpDataReceivedEventArgs>? OnDataReceived;

    /// <summary>Raised for accept, pipeline and connection errors.</summary>
    public event EventHandler<TcpExceptionEventArgs>? OnException;

    /// <summary>
    /// Gets a snapshot of the bound endpoint while running or draining, or the configured endpoint otherwise.
    /// An ephemeral port is resolved after startup. Changes to the snapshot do not affect the listener.
    /// </summary>
    public IPEndPoint Endpoint
    {
        get
        {
            lock (_lifecycleSync)
            {
                var endpoint = _state == TcpServerState.Running && _boundEndPoint is { } boundEndPoint
                                   ? boundEndPoint
                                   : _endPoint;

                return (IPEndPoint)endpoint.Create(endpoint.Serialize());
            }
        }
    }

    /// <inheritdoc />
    public int Port
    {
        get
        {
            lock (_lifecycleSync)
            {
                return _port;
            }
        }
    }

    /// <summary>Gets whether the listener is accepting connections; false during graceful drain.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_lifecycleSync)
            {
                return _state == TcpServerState.Running && _admissionStopTask is null;
            }
        }
    }

    /// <summary>
    /// Creates a listener. Shared middleware and framers must be stateless or thread-safe;
    /// use the pipeline factory for connection-specific state.
    /// </summary>
    public MoongateTcpServer(
        IPEndPoint endPoint,
        INetFramer? framer = null,
        int receiveBufferSize = 8192,
        Func<ConnectionPipeline>? connectionPipelineFactory = null,
        int maxFrameLength = 1024 * 1024,
        bool noDelay = true
    )
    {
        if (receiveBufferSize is < 1 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
        }

        if (maxFrameLength is < 1 or > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameLength));
        }

        _endPoint = endPoint;
        _framer = framer;
        _receiveBufferSize = receiveBufferSize;
        _connectionPipelineFactory = connectionPipelineFactory;
        _maxFrameLength = maxFrameLength;
        _noDelay = noDelay;
    }

    private MoongateTcpServer(IPEndPoint endpoint, TcpServerOptions options)
        : this(
            endpoint,
            options.Framer,
            options.ReceiveBufferSize,
            options.ConnectionPipelineFactory,
            options.MaxFrameLength,
            options.NoDelay
        )
    {
        _configuredOptions = options;
    }

    /// <summary>Registers middleware in execution order.</summary>
    public MoongateTcpServer AddMiddleware(INetMiddleware middleware)
    {
        lock (_middlewareSync)
        {
            _middlewares = [.. _middlewares, middleware];
        }

        return this;
    }

    /// <summary>Creates a listener with bounded, asynchronous stream preparation.</summary>
    public static MoongateTcpServer CreateConfigured(IPEndPoint endpoint, TcpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return new(endpoint, options);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task wait;
            TaskCompletionSource? start = null;
            bool stopping;

            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_disposeRequested, this);
                cancellationToken.ThrowIfCancellationRequested();

                if (_state == TcpServerState.Running)
                {
                    return;
                }

                stopping = _state == TcpServerState.Stopping;

                if (_state == TcpServerState.Stopped)
                {
                    start = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    _startTask = start.Task;
                    _state = TcpServerState.Starting;
                    _admissionStopTask = null;
                }

                wait = stopping ? _stopTask : _startTask;
            }

            if (start is not null)
            {
                StartCore(start, cancellationToken);
            }

            await wait.WaitAsync(cancellationToken);

            if (!stopping)
            {
                return;
            }
        }
    }

    /// <summary>Closes the listener and cancels pending preparation while keeping established clients usable.</summary>
    /// <remarks>Call StopAsync before restarting. Caller cancellation only cancels the wait.</remarks>
    public Task StopAcceptingAsync(CancellationToken cancellationToken = default)
    {
        Task stop;

        lock (_lifecycleSync)
        {
            if (_state is TcpServerState.Stopped or TcpServerState.Disposed)
            {
                return Task.CompletedTask;
            }

            stop = _admissionStopTask ??= StopAcceptingCoreAsync();
        }

        return cancellationToken.CanBeCanceled ? stop.WaitAsync(cancellationToken) : stop;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        var cleanup = GetOrStartStopTask();

        return cancellationToken.CanBeCanceled ? cleanup.WaitAsync(cancellationToken) : cleanup;
    }

    private async Task AcceptLoopAsync(Socket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket accepted;

            try
            {
                accepted = await socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested ||
                                              exception is ObjectDisposedException)
            {
                break;
            }
            catch (SocketException exception)
            {
                ReportException(new(exception));

                try
                {
                    await Task.Delay(AcceptRetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            if (_configuredOptions is not null)
            {
                AdmitPreparation(accepted, cancellationToken);

                continue;
            }

            MoongateTcpClient? client = null;
            TaskCompletionSource? started = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pipeline = _connectionPipelineFactory?.Invoke();
                client = new(
                    accepted,
                    pipeline?.Middlewares ?? Volatile.Read(ref _middlewares),
                    pipeline?.Framer ?? _framer,
                    pipeline?.Codec,
                    _receiveBufferSize,
                    _maxFrameLength,
                    _noDelay
                );
                WireClientEvents(client);
                started = new(TaskCreationOptions.RunContinuationsAsynchronously);

                lock (_lifecycleSync)
                {
                    _clients.Add(client.SessionId, client);
                    _clientStarts.Add(client.SessionId, started);
                }

                // The server owns generation shutdown: drain accept before requesting client closes.
                await client.StartAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (client is null)
                {
                    accepted.Dispose();
                }
                else
                {
                    _ = GetOrStartClientCleanup(client);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    ReportException(new(exception, client));
                }
            }
            finally
            {
                started?.TrySetResult();
            }
        }
    }

    private void AdmitPreparation(Socket socket, CancellationToken generationToken)
    {
        AcceptedConnectionSetup? setup = null;

        lock (_lifecycleSync)
        {
            if (_state == TcpServerState.Running &&
                !generationToken.IsCancellationRequested &&
                _admittedConnections < _configuredOptions!.MaxConnections &&
                _preparingConnections < _configuredOptions.MaxConcurrentPreparations)
            {
                setup = new(socket);
                _admittedConnections++;
                _preparingConnections++;
                _setups.Add(setup);
            }
        }

        if (setup is null)
        {
            socket.Dispose();

            return;
        }

        _ = PrepareAcceptedAsync(setup, generationToken);
    }

    private async Task CleanupClientAsync(MoongateTcpClient client, Task start, TaskCompletionSource completion)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            await start.ConfigureAwait(false);
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (_lifecycleSync)
            {
                _cleanupErrors.Add(exception);
            }

            _logger.Error(exception, "Connection cleanup failed for {SessionId}", client.SessionId);
        }
        finally
        {
            lock (_lifecycleSync)
            {
                if (_clients.Remove(client.SessionId) && _configuredOptions is not null)
                {
                    _admittedConnections--;
                }

                _clientStarts.Remove(client.SessionId);
                _clientCleanups.Remove(client.SessionId);
                completion.TrySetResult();
            }
        }
    }

    private Task GetOrStartClientCleanup(MoongateTcpClient client)
    {
        TaskCompletionSource completion;
        Task start;

        lock (_lifecycleSync)
        {
            if (_clientCleanups.TryGetValue(client.SessionId, out var cleanup))
            {
                return cleanup;
            }

            if (!_clientStarts.TryGetValue(client.SessionId, out var started))
            {
                return Task.CompletedTask;
            }

            start = started.Task;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _clientCleanups.Add(client.SessionId, completion.Task);
        }

        _ = CleanupClientAsync(client, start, completion);

        return completion.Task;
    }

    private Task GetOrStartStopTask(bool dispose = false)
    {
        TaskCompletionSource? completion = null;
        Task result;

        lock (_lifecycleSync)
        {
            _disposeRequested |= dispose;

            if (_state is TcpServerState.Stopped or TcpServerState.Disposed)
            {
                if (_disposeRequested)
                {
                    _state = TcpServerState.Disposed;
                }

                return _stopTask;
            }

            if (_state != TcpServerState.Stopping)
            {
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _stopTask = completion.Task;
                _state = TcpServerState.Stopping;
                _port = 0;
            }

            result = _stopTask;
        }

        if (completion is not null)
        {
            _ = StopCoreAsync(completion);

            // Cancellation and synchronous Dispose may be the only callers. Observe failure even
            // then; the shared task still carries it to subsequent Stop/DisposeAsync callers.
            _ = result.ContinueWith(
                task => _logger.Error(task.Exception, "TCP cleanup failed"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default
            );
        }

        return result;
    }

    private void InvokeSafely<T>(EventHandler<T>? handlers, T args) where T : EventArgs
    {
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<T> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "TCP diagnostic or disconnect handler failed");
            }
        }
    }

    private async Task PrepareAcceptedAsync(AcceptedConnectionSetup setup, CancellationToken generationToken)
    {
        // Start asynchronously so even a synchronously completing preparer cannot block accept.
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        Stream? stream = null;
        MoongateTcpClient? client = null;
        TaskCompletionSource? started = null;
        var promoted = false;
        var options = _configuredOptions!;
        using var deadline = new CancellationTokenSource(options.PreparationTimeout, options.TimeProvider);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(generationToken, deadline.Token);

        try
        {
            lifetime.Token.ThrowIfCancellationRequested();
            var pipeline = _connectionPipelineFactory?.Invoke() ?? new ConnectionPipeline();
            stream = new NetworkStream(setup.Socket, false);

            if (pipeline.PrepareStreamAsync is { } prepare)
            {
                var prepared = await prepare(stream, lifetime.Token).ConfigureAwait(false);
                stream = prepared ?? throw new InvalidOperationException("Preparation returned no stream.");
            }

            lifetime.Token.ThrowIfCancellationRequested();

            if (!stream.CanRead || !stream.CanWrite)
            {
                throw new InvalidOperationException("Preparation must return a readable and writable stream.");
            }

            client = new(
                setup.Socket,
                stream,
                pipeline.Middlewares ?? Volatile.Read(ref _middlewares),
                pipeline.Framer ?? _framer,
                pipeline.Codec,
                _receiveBufferSize,
                _maxFrameLength,
                _noDelay
            );
            started = new(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_lifecycleSync)
            {
                generationToken.ThrowIfCancellationRequested();

                if (_state != TcpServerState.Running)
                {
                    throw new OperationCanceledException(generationToken);
                }

                _clients.Add(client.SessionId, client);
                _clientStarts.Add(client.SessionId, started);
                _preparingConnections--;
                promoted = true;
            }

            WireClientEvents(client);
            pipeline.ConfigureClient?.Invoke(client);
            await client.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (promoted)
            {
                _ = GetOrStartClientCleanup(client!);
            }

            if (!generationToken.IsCancellationRequested)
            {
                ReportException(new(exception, client));
            }
        }
        finally
        {
            started?.TrySetResult();

            if (!promoted)
            {
                try
                {
                    if (client is not null)
                    {
                        await client.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        try
                        {
                            if (stream is not null)
                            {
                                await stream.DisposeAsync().ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            setup.Socket.Dispose();
                        }
                    }
                }
                catch (Exception exception)
                {
                    lock (_lifecycleSync)
                    {
                        _cleanupErrors.Add(exception);
                    }
                }
            }

            lock (_lifecycleSync)
            {
                if (!promoted)
                {
                    _admittedConnections--;
                    _preparingConnections--;
                }

                _setups.Remove(setup);
                setup.Completion.TrySetResult();
            }
        }
    }

    private void ReportException(TcpExceptionEventArgs args)
    {
        _logger.Error(args.Exception, "TCP transport failure");
        InvokeSafely(OnException, args);
    }

    private async Task RunAcceptLoopAsync(
        Socket socket,
        TaskCompletionSource completion,
        CancellationToken cancellationToken
    )
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            await AcceptLoopAsync(socket, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            ReportException(new(exception));
        }
        finally
        {
            completion.TrySetResult();
        }
    }

    private void StartCore(TaskCompletionSource completion, CancellationToken cancellationToken)
    {
        Socket? socket = null;
        CancellationTokenSource? lifetime = null;
        CancellationTokenRegistration registration = default;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            socket = new(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(_endPoint);
            socket.Listen(DefaultBacklog);
            lifetime = new();
            cancellationToken.ThrowIfCancellationRequested();
            registration = cancellationToken.Register(() => GetOrStartStopTask());
            var boundSocket = socket;
            var generation = lifetime;
            var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var boundEndPoint = (IPEndPoint)socket.LocalEndPoint!;

            lock (_lifecycleSync)
            {
                _serverSocket = socket;
                _listenerCancellationTokenSource = lifetime;
                _startCancellationRegistration = registration;
                _acceptLoopTask = accepted.Task;

                if (_state == TcpServerState.Starting)
                {
                    _state = TcpServerState.Running;
                    _boundEndPoint = boundEndPoint;
                    _port = boundEndPoint.Port;
                }
            }

            _ = RunAcceptLoopAsync(boundSocket, accepted, generation.Token);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            registration.Dispose();
            socket?.Dispose();
            lifetime?.Dispose();

            lock (_lifecycleSync)
            {
                if (_state == TcpServerState.Starting)
                {
                    _state = TcpServerState.Stopped;
                }
            }

            if (exception is OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            else
            {
                completion.TrySetException(exception);
            }
        }
    }

    private async Task StopAcceptingCoreAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            await _startTask.ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        _serverSocket?.Dispose();

        if (_listenerCancellationTokenSource is { } lifetime)
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
        }

        await _acceptLoopTask.ConfigureAwait(false);
        AcceptedConnectionSetup[] setups;

        lock (_lifecycleSync)
        {
            setups = _setups.ToArray();
        }

        foreach (var setup in setups)
        {
            setup.Socket.Dispose();
        }

        await Task.WhenAll(setups.Select(setup => setup.Completion.Task)).ConfigureAwait(false);
    }

    private async Task StopCoreAsync(TaskCompletionSource completion)
    {
        // Never execute cancellation or user handlers on a synchronous Dispose caller's stack.
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        var errors = new List<Exception>();

        try
        {
            try
            {
                await _startTask.ConfigureAwait(false);
            }
            catch
            {
                // Failed startup already released its unpublished resources.
            }

            var lifetime = _listenerCancellationTokenSource;

            try
            {
                await StopAcceptingAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            MoongateTcpClient[] clients;

            lock (_lifecycleSync)
            {
                clients = _clients.Values.ToArray();
            }

            // Request all closes before waiting on any receive/send/resource release.
            foreach (var client in clients)
            {
                try
                {
                    await client.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            var releases = clients.Select(GetOrStartClientCleanup).ToArray();
            await Task.WhenAll(releases).ConfigureAwait(false);

            try
            {
                await _startCancellationRegistration.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                lifetime?.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
        finally
        {
            lock (_lifecycleSync)
            {
                errors.AddRange(_cleanupErrors);
                _cleanupErrors.Clear();
                _boundEndPoint = null;
                _serverSocket = null;
                _listenerCancellationTokenSource = null;
                _startCancellationRegistration = default;
                _acceptLoopTask = Task.CompletedTask;
                _state = _disposeRequested ? TcpServerState.Disposed : TcpServerState.Stopped;
            }

            if (errors.Count == 0)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(new AggregateException(errors));
            }
        }
    }

    private void WireClientEvents(MoongateTcpClient client)
    {
        client.OnConnected += (_, args) => OnClientConnect?.Invoke(this, args);
        client.OnDataReceived += (_, args) => OnDataReceived?.Invoke(this, args);
        client.OnException += (_, args) => ReportException(args);
        client.OnDisconnected += (_, args) =>
                                 {
                                     _ = GetOrStartClientCleanup(client);
                                     InvokeSafely(OnClientDisconnect, args);
                                 };
    }

    /// <summary>Requests terminal shutdown without blocking the current callback.</summary>
    public void Dispose()
        => _ = GetOrStartStopTask(true);

    /// <summary>Requests terminal shutdown and waits for all owned resources.</summary>
    public async ValueTask DisposeAsync()
        => await GetOrStartStopTask(true).ConfigureAwait(false);
}
