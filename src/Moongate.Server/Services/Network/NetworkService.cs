using System.Net;
using System.Net.Sockets;
using Moongate.Network.Data.Events;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Server;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Core.Data.Network.Events;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Services.Network;

/// <summary>Owns transport listeners and synchronous notifications independently of game state.</summary>
public sealed class NetworkService : INetworkService, ILoginNetworkService
{
    private readonly ILogger _logger = Log.ForContext<NetworkService>();
    private readonly IConnectionService _connections;
    private readonly Lock _cleanupGate = new();
    private readonly HashSet<long> _admitted = [];
    private readonly HashSet<Task> _cleanups = [];
    private readonly List<Exception> _failures = [];
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly Lock _lifecycleGate = new();
    private bool _stopping;
    public event EventHandler<NetworkConnectionEventArgs>? ConnectionAccepted;
    public event EventHandler<NetworkConnectionEventArgs>? ConnectionClosed;
    public event EventHandler<NetworkDataEventArgs>? DataReceived;

    internal IReadOnlyList<MoongateTcpServer> Listeners { get; }

    public NetworkService(NetworkListenerOptions options, IConnectionService connections)
        : this(CreateListeners(options), connections) { }

    internal NetworkService(IReadOnlyList<MoongateTcpServer> listeners, IConnectionService connections)
    {
        _connections = connections;
        Listeners = listeners.ToArray();

        foreach (var listener in Listeners)
        {
            listener.OnClientConnect += OnClientConnect;
            listener.OnClientDisconnect += OnClientDisconnect;
            listener.OnDataReceived += OnDataReceived;
        }
    }

    public Task StartAsync()
    {
        lock (_lifecycleGate)
        {
            if (_stopping)
            {
                throw new InvalidOperationException("Network listeners cannot start after shutdown begins.");
            }

            return _lifecycle.StartAsync(StartCoreAsync);
        }
    }

    public Task StopAsync()
    {
        lock (_lifecycleGate)
        {
            _stopping = true;

            return _lifecycle.StopAsync(
                async startup =>
                {
                    if (startup is not null)
                    {
                        await startup.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    }

                    await StopListenersAsync().ConfigureAwait(false);
                }
            );
        }
    }

    private static async Task CaptureCloseAsync(INetworkConnection connection)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        await connection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task CloseRejectedAsync(INetworkConnection connection)
    {

        // A close request is distinct from actual completion; join both even when one fails.
        await Task.WhenAll(CaptureCloseAsync(connection), connection.Completion).ConfigureAwait(false);
    }

    private static MoongateTcpServer[] CreateListeners(NetworkListenerOptions options)
    {
        if (options.Endpoints is null || options.Endpoints.Count == 0)
        {
            throw new ArgumentException("At least one listener endpoint is required.", nameof(options));
        }

        return options.Endpoints
                      .Select(
                          endpoint =>
                          {
                              ArgumentNullException.ThrowIfNull(endpoint);
                              var address = endpoint.Address.AddressFamily == AddressFamily.InterNetworkV6
                                                ? new IPAddress(endpoint.Address.GetAddressBytes(), endpoint.Address.ScopeId)
                                                : new IPAddress(endpoint.Address.GetAddressBytes());

                              return new MoongateTcpServer(
                                  new(address, endpoint.Port),
                                  connectionPipelineFactory: options.ConnectionPipelineFactory
                              );
                          }
                      )
                      .ToArray();
    }

    private void OnClientConnect(object? sender, TcpClientEventArgs args)
    {
        if (!_connections.TryRegister(args.Client))
        {
            TrackCleanup(() => CloseRejectedAsync(args.Client));

            return;
        }

        lock (_cleanupGate)
        {
            _admitted.Add(args.Client.SessionId);
        }

        Publish(ConnectionAccepted, new(args.Client), args.Client, true);
    }

    private void OnClientDisconnect(object? sender, TcpClientEventArgs args)
    {
        lock (_cleanupGate)
        {
            if (!_admitted.Remove(args.Client.SessionId))
            {
                return;
            }
        }

        Publish(ConnectionClosed, new(args.Client), args.Client, false);
        TrackCleanup(() => _connections.DisconnectAsync(args.Client.SessionId));
    }

    private void OnDataReceived(object? sender, TcpDataReceivedEventArgs args)
    {
        if (_connections.TryGet(args.Client.SessionId, out _))
        {
            Publish(DataReceived, new(args.Client, args.Data), args.Client, true);
        }
    }

    private void Publish<T>(EventHandler<T>? handlers, T args, INetworkConnection connection, bool closeOnFailure)
        where T : EventArgs
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
                _logger.Error(exception, "Network callback failed for connection {SessionId}", connection.SessionId);

                if (closeOnFailure)
                {
                    TrackCleanup(() => _connections.DisconnectAsync(connection.SessionId));
                }
            }
        }
    }

    private async Task RunCleanupAsync(Func<Task> cleanup, TaskCompletionSource completion)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (_cleanupGate)
            {
                _failures.Add(exception);
            }

            _logger.Error(exception, "Transport connection cleanup failed");
        }
        finally
        {
            lock (_cleanupGate)
            {
                completion.TrySetResult();
                _cleanups.Remove(completion.Task);
            }
        }
    }

    private async Task StartCoreAsync()
    {
        try
        {
            foreach (var listener in Listeners)
            {
                _logger.Information(
                    "Starting TCP server on {Address}:{Port}",
                    listener.Endpoint.Address,
                    listener.Endpoint.Port
                );
                await listener.StartAsync(default).ConfigureAwait(false);
            }
        }
        catch
        {
            lock (_lifecycleGate)
            {
                _stopping = true;
            }

            try
            {
                await StopListenersAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "TCP cleanup failed after listener startup failure");
            }

            throw;
        }
    }

    private async Task StopListenersAsync()
    {
        List<Exception> failures = [];
        await Task.WhenAll(
                      Listeners.Select(
                          async listener =>
                          {
                              try
                              {
                                  await listener.StopAsync(default).ConfigureAwait(false);
                              }
                              catch (Exception exception)
                              {
                                  lock (failures)
                                  {
                                      failures.Add(exception);
                                  }
                              }
                          }
                      )
                  )
                  .ConfigureAwait(false);
        Task[] pending;

        lock (_cleanupGate)
        {
            pending = _cleanups.ToArray();
        }

        // Listener stop has joined callbacks, so no new cleanup can be published.
        await Task.WhenAll(pending).ConfigureAwait(false);

        lock (_cleanupGate)
        {
            failures.AddRange(_failures);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }

    private void TrackCleanup(Func<Task> cleanup)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_cleanupGate)
        {
            _cleanups.Add(completion.Task);
        }

        _ = RunCleanupAsync(cleanup, completion);
    }
}
