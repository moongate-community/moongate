using System.Net;
using Moongate.Api.Connections.Internal;
using Moongate.Api.Data.Config;
using Moongate.Api.Data.Security;
using Moongate.Api.Exceptions;
using Moongate.Api.Framing;
using Moongate.Api.Hosting.Internal;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Interfaces.Server;
using Moongate.Api.Registry;
using Moongate.Api.Security.Internal;
using Moongate.Api.Streams.Internal;
using Moongate.Network.Client;
using Moongate.Network.Data;
using Moongate.Network.Server;
using Serilog;

namespace Moongate.Api.Server;

/// <summary>Standalone mutual-TLS API listener with locally authorized typed operations.</summary>
public sealed class ApiServer : IApiServer
{
    private static readonly ILogger Logger = Log.ForContext<ApiServer>();
    private readonly Lock _gate = new();
    private readonly IPEndPoint _endpoint;
    private readonly ApiRegistry _registry;
    private readonly ApiOptions _options;
    private readonly ApiTlsPolicy _tls;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _slots;
    private readonly HashSet<ApiConnection> _connections = [];
    private MoongateTcpServer? _tcp;
    private Task _start = Task.CompletedTask;
    private Task? _stopCompletion;
    private Task? _stopResult;
    private Task? _disposeCompletion;
    private bool _running;
    private bool _stopping;
    private bool _disposeRequested;
    private int _admitted;

    public IPEndPoint? Endpoint
    {
        get
        {
            lock (_gate)
            {
                return _running && !_stopping ? _tcp!.Endpoint : null;
            }
        }
    }

    public IReadOnlyList<IApiConnection> Connections
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(
                    _connections.Where(connection => connection.IsConnected).Cast<IApiConnection>().ToArray()
                );
            }
        }
    }

    public ApiServer(
        IPEndPoint endpoint,
        ApiRegistry registry,
        ApiOptions options,
        ApiTlsOptions tls,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options.Validate();
        registry.Freeze();
        _endpoint = new(endpoint.Address, endpoint.Port);
        _registry = registry;
        _options = options with { };
        _clock = timeProvider;
        _tls = new(tls);
        _slots = new(options.MaxConcurrentHandlers, options.MaxConcurrentHandlers);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Task start;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            cancellationToken.ThrowIfCancellationRequested();

            if (_running && !_stopping)
            {
                return Task.CompletedTask;
            }

            if (_stopCompletion is { IsCompleted: false })
            {
                throw new InvalidOperationException("Previous API generation is still shutting down.");
            }

            if (_start.IsCompleted)
            {
                _stopping = false;
                _stopCompletion = null;
                _stopResult = null;
                _start = StartCoreAsync();
            }

            start = _start;
        }

        return cancellationToken.CanBeCanceled ? start.WaitAsync(cancellationToken) : start;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stop;

        lock (_gate)
        {
            BeginStop();
            stop = _stopCompletion!.IsCompleted ? _stopCompletion : _stopResult!;
        }

        return cancellationToken.CanBeCanceled ? stop.WaitAsync(cancellationToken) : stop;
    }

    private void BeginStop()
    {
        _stopping = true;
        _running = false;

        if (_stopCompletion is not null)
        {
            return;
        }

        _stopCompletion = StopCoreAsync();
        _stopResult = ApiShutdown.WaitAsync(_stopCompletion, ForceClose, _options.ShutdownTimeout, _clock, Remaining);
        _ = ApiShutdown.ObserveAsync(_stopCompletion);
        _ = ApiShutdown.ObserveAsync(_stopResult);
    }

    private void Configure(MoongateTcpClient transport, ApiPeerIdentity peer, ApiConnectionAdmission admission)
    {
        ApiConnection connection;

        lock (_gate)
        {
            if (_stopping || _disposeRequested)
            {
                throw new IOException("The API listener is stopping.");
            }

            admission.TransferToConnection();

            try
            {
                connection = new(transport, peer, _registry, _options, _slots, _clock, admission);
            }
            catch
            {
                admission.Dispose();

                throw;
            }

            transport.OnDataReceived += (_, args) => connection.Receive(args.Data);
            transport.OnDisconnected += (_, _) => _ = connection.CloseAsync();
            _connections.Add(connection);
        }

        _ = RemoveCompletedAsync(connection);
    }

    private ConnectionPipeline CreatePipeline()
    {
        ApiConnectionAdmission? admission = null;
        var setup = new ApiConnectionSetup(
            _tls,
            new ApiFrameFramer(_options.MaxFrameLength),
            (transport, peer) => Configure(transport, peer, admission ?? throw new IOException("Missing API admission."))
        );

        return setup.Pipeline with
        {
            PrepareStreamAsync = async (stream, token) =>
                                 {
                                     admission = ReserveAdmission();
                                     var owned = new ApiAdmissionStream(stream, admission);

                                     try
                                     {
                                         return await setup.Pipeline.PrepareStreamAsync!(owned, token).ConfigureAwait(false);
                                     }
                                     catch
                                     {
                                         await owned.DisposeAsync().ConfigureAwait(false);

                                         throw;
                                     }
                                 }
        };
    }

    private async Task DisposeCoreAsync(Task stop)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            await stop.ConfigureAwait(false);
        }
        finally
        {
            _tls.Dispose();
            _slots.Dispose();
        }
    }

    private void ForceClose()
    {
        ApiConnection[] connections;
        MoongateTcpServer? tcp;

        lock (_gate)
        {
            connections = _connections.ToArray();
            tcp = _tcp;
        }

        foreach (var connection in connections)
        {
            _ = connection.CloseAsync();
        }

        if (tcp is not null)
        {
            _ = ApiShutdown.ObserveAsync(tcp.StopAsync(CancellationToken.None));
        }
    }

    private int Remaining()
    {
        lock (_gate)
        {
            return _admitted;
        }
    }

    private async Task RemoveCompletedAsync(ApiConnection connection)
    {
        await ApiShutdown.ObserveAsync(connection.Completion).ConfigureAwait(false);

        lock (_gate)
        {
            _connections.Remove(connection);
        }
    }

    private ApiConnectionAdmission ReserveAdmission()
    {
        lock (_gate)
        {
            if (_stopping || _disposeRequested)
            {
                throw new IOException("The API listener is stopping.");
            }

            if (_admitted >= _options.MaxConnections)
            {
                throw new ApiBusyException();
            }

            _admitted++;
        }

        return new(
            () =>
            {
                lock (_gate)
                {
                    _admitted--;
                }
            }
        );
    }

    private async Task StartCoreAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        var tcp = MoongateTcpServer.CreateConfigured(
            _endpoint,
            new()
            {
                MaxConnections = _options.MaxConnections,
                MaxConcurrentPreparations = _options.MaxConcurrentHandshakes,
                PreparationTimeout = _options.HandshakeTimeout,
                MaxFrameLength = _options.MaxFrameLength + 4,
                TimeProvider = _clock,
                ConnectionPipelineFactory = CreatePipeline
            }
        );

        lock (_gate)
        {
            _tcp = tcp;
        }

        try
        {
            await tcp.StartAsync(CancellationToken.None).ConfigureAwait(false);

            lock (_gate)
            {
                _running = !_stopping;
            }

            Logger.Information(
                "API listener started at {Endpoint}: {ContractCount} contracts, {HandlerCount} handlers",
                tcp.Endpoint,
                _registry.ContractCount,
                _registry.HandlerCount
            );
        }
        catch
        {
            await tcp.DisposeAsync().ConfigureAwait(false);

            lock (_gate)
            {
                if (ReferenceEquals(_tcp, tcp))
                {
                    _tcp = null;
                }
            }

            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            await _start.ConfigureAwait(false);
        }

        catch { }

        MoongateTcpServer? tcp;

        lock (_gate)
        {
            tcp = _tcp;
        }

        if (tcp is null)
        {
            return;
        }

        await tcp.StopAcceptingAsync().ConfigureAwait(false);
        ApiConnection[] connections;

        lock (_gate)
        {
            connections = _connections.ToArray();
        }

        try
        {
            await Task.WhenAll(connections.Select(connection => connection.DrainAsync())).ConfigureAwait(false);
        }
        finally
        {
            await tcp.DisposeAsync().ConfigureAwait(false);
        }

        lock (_gate)
        {
            if (ReferenceEquals(_tcp, tcp))
            {
                _tcp = null;
            }
        }

        Logger.Information("API listener stopped");
    }

    public ValueTask DisposeAsync()
    {
        Task dispose;

        lock (_gate)
        {
            _disposeRequested = true;
            BeginStop();
            _disposeCompletion ??= DisposeCoreAsync(_stopCompletion!);
            _ = ApiShutdown.ObserveAsync(_disposeCompletion);
            dispose = _disposeCompletion;
        }

        return new(ApiShutdown.WaitAsync(dispose, ForceClose, _options.ShutdownTimeout, _clock, Remaining));
    }
}
