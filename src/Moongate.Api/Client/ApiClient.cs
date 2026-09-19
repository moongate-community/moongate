using System.Net;
using Moongate.Api.Connections.Internal;
using Moongate.Api.Data.Config;
using Moongate.Api.Exceptions;
using Moongate.Api.Framing;
using Moongate.Api.Hosting.Internal;
using Moongate.Api.Interfaces.Client;
using Moongate.Api.Interfaces.Connections;
using Moongate.Api.Registry;
using Moongate.Api.Security.Internal;
using Moongate.Network.Client;
using Serilog;

namespace Moongate.Api.Client;

/// <summary>Owns explicit outgoing API connections and aggregate connection, handshake and execution budgets.</summary>
public sealed class ApiClient : IApiClient
{
    private static readonly ILogger Logger = Log.ForContext<ApiClient>();
    private readonly object _gate = new();
    private readonly ApiRegistry _registry;
    private readonly ApiOptions _options;
    private readonly ApiTlsPolicy _tls;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _slots;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HashSet<ApiConnection> _connections = [];
    private readonly HashSet<TaskCompletionSource> _setups = [];
    private Task? _disposeCompletion;
    private Task? _disposeResult;
    private int _admitted;
    private bool _closing;

    public ApiClient(ApiRegistry registry, ApiOptions options, ApiTlsOptions tls, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options.Validate();
        registry.Freeze();
        _registry = registry;
        _options = options with { };
        _clock = timeProvider;
        _tls = new(tls);
        _slots = new(options.MaxConcurrentHandlers, options.MaxConcurrentHandlers);
    }

    public async Task<IApiConnection> ConnectAsync(
        IPEndPoint endpoint,
        string targetHost,
        string expectedPeerId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPeerId);
        cancellationToken.ThrowIfCancellationRequested();
        var setup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closing, this);

            if (_admitted >= _options.MaxConnections || _setups.Count >= _options.MaxConcurrentHandshakes)
            {
                throw new ApiBusyException();
            }
            _admitted++;
            _setups.Add(setup);
        }
        ApiConnection? connection = null;

        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
            var pipeline = new ApiConnectionSetup(
                _tls,
                new ApiFrameFramer(_options.MaxFrameLength),
                (transport, peer) =>
                {
                    lock (_gate)
                    {
                        if (_closing) { throw new IOException("The API client is stopping."); }
                        connection = new(transport, peer, _registry, _options, _slots, _clock);
                        var configured = connection;
                        transport.OnDataReceived += (_, args) => configured.Receive(args.Data);
                        transport.OnDisconnected += (_, _) => _ = configured.CloseAsync();
                        _connections.Add(connection);
                    }
                    _ = RemoveCompletedAsync(connection);
                },
                targetHost,
                expectedPeerId
            ).Pipeline;
            var transport = await MoongateTcpClient.ConnectConfiguredAsync(
                                                       endpoint,
                                                       new()
                                                       {
                                                           Pipeline = pipeline,
                                                           MaxFrameLength = _options.MaxFrameLength + 4,
                                                           PreparationTimeout = _options.HandshakeTimeout,
                                                           TimeProvider = _clock
                                                       },
                                                       cancellation.Token
                                                   )
                                                   .ConfigureAwait(false);

            if (connection is null || !transport.IsConnected || connection.Completion.IsCompleted)
            {
                throw new IOException("The API connection closed during startup.");
            }
            Logger.Information(
                "API connected to {Endpoint} as peer {PeerId}: {ContractCount} contracts, {HandlerCount} handlers",
                endpoint,
                connection.Peer.PeerId,
                _registry.ContractCount,
                _registry.HandlerCount
            );

            return connection;
        }
        finally
        {
            lock (_gate)
            {
                _setups.Remove(setup);

                if (connection is null) { _admitted--; }
                setup.TrySetResult();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Task result;

        lock (_gate)
        {
            _closing = true;

            if (_disposeCompletion is null)
            {
                _disposeCompletion = DisposeCoreAsync();
                _disposeResult = ApiShutdown.WaitAsync(
                    _disposeCompletion,
                    ForceClose,
                    _options.ShutdownTimeout,
                    _clock,
                    Remaining
                );
                _ = ApiShutdown.ObserveAsync(_disposeCompletion);
                _ = ApiShutdown.ObserveAsync(_disposeResult);
            }
            result = _disposeCompletion.IsCompleted ? _disposeCompletion : _disposeResult!;
        }

        return new(result);
    }

    private async Task DisposeCoreAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        await _shutdown.CancelAsync().ConfigureAwait(false);
        Task[] setups;

        lock (_gate) { setups = _setups.Select(setup => setup.Task).ToArray(); }
        await Task.WhenAll(setups).ConfigureAwait(false);
        ApiConnection[] connections;

        lock (_gate) { connections = _connections.ToArray(); }

        try { await Task.WhenAll(connections.Select(connection => connection.DrainAsync())).ConfigureAwait(false); }
        finally
        {
            _tls.Dispose();
            _slots.Dispose();
            _shutdown.Dispose();
        }
    }

    private void ForceClose()
    {
        ApiConnection[] connections;

        lock (_gate) { connections = _connections.ToArray(); }

        foreach (var connection in connections) { _ = connection.CloseAsync(); }
    }

    private int Remaining()
    {
        lock (_gate) { return _admitted; }
    }

    private async Task RemoveCompletedAsync(ApiConnection connection)
    {
        await ApiShutdown.ObserveAsync(connection.Completion).ConfigureAwait(false);

        lock (_gate)
        {
            if (_connections.Remove(connection)) { _admitted--; }
        }
    }
}
