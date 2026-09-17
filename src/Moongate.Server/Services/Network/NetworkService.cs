using System.Net;
using Moongate.Core.Utils;
using Moongate.Network.Data.Events;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Server;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Network.Framing;
using Serilog;

namespace Moongate.Server.Services.Network;

public class NetworkService : INetworkService
{
    private readonly ILogger _logger = Log.ForContext<NetworkService>();
    private readonly ISessionService _sessionService;
    private readonly IPacketDispatchService _dispatcher;
    private readonly IPacketSendService _sender;
    private readonly Lock _cleanupGate = new();
    private readonly HashSet<Task> _cleanups = [];
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly Lock _lifecycleGate = new();

    private bool _stopping;

    internal IReadOnlyList<MoongateTcpServer> Listeners { get; }

    public NetworkService(
        MoongateServerConfig config,
        ISessionService sessionService,
        IPacketDispatchService dispatcher,
        IPacketSendService sender
    )
        : this(CreateListeners(config), sessionService, dispatcher, sender) { }

    internal NetworkService(
        IReadOnlyList<MoongateTcpServer> listeners,
        ISessionService sessionService,
        IPacketDispatchService dispatcher,
        IPacketSendService sender
    )
    {
        _sessionService = sessionService;
        _dispatcher = dispatcher;
        _sender = sender;
        Listeners = listeners;

        foreach (var listener in Listeners)
        {
            listener.OnClientConnect += TcpServerOnOnClientConnect;
            listener.OnClientDisconnect += TcpServerOnOnClientDisconnect;
            listener.OnDataReceived += TcpServerOnOnDataReceived;
        }
    }

    private static IReadOnlyList<MoongateTcpServer> CreateListeners(MoongateServerConfig config)
    {
        var addresses = config.Network.ListenAddress == "0.0.0.0"
                            ? new List<IPAddress>(NetworkUtils.GetLocalIpAddresses())
                            : new List<IPAddress> { IPAddress.Parse(config.Network.ListenAddress) };

        return addresses.Select(
                            address => new MoongateTcpServer(
                                new IPEndPoint(address, config.Network.GamePort),
                                framer: new UoPacketFramer(PacketRegistry.Default)
                            )
                        )
                        .ToArray();
    }

    private void TcpServerOnOnDataReceived(object? sender, TcpDataReceivedEventArgs e)
    {
        if (PacketRegistry.Default.TryDecode(e.Data.Span, out var packet, out var opCode) &&
            _dispatcher.TryDispatch(e.Client.SessionId, packet))
        {
            return;
        }

        var packetName = PacketRegistry.Default.TryGetDescriptor(opCode, out var descriptor)
                             ? descriptor.PacketType.Name
                             : "Unknown";
        _logger.Warning(
            "Rejected packet from session {SessionId}, opcode {OpCode}, name {PacketName}",
            e.Client.SessionId,
            opCode,
            packetName
        );
        e.Client.Dispose();
    }

    private void TcpServerOnOnClientDisconnect(object? sender, TcpClientEventArgs e)
    {
        // Completion follows this synchronous event. Publish ownership before starting cleanup,
        // and return without waiting for either the client completion or the game loop.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_cleanupGate)
        {
            _cleanups.Add(completion.Task);
        }
        _ = CleanupConnectionAsync(e.Client.SessionId, completion);
    }

    private async Task CleanupConnectionAsync(long sessionId, TaskCompletionSource completion)
    {
        try
        {
            // Start both operations even if the first throws synchronously or fails asynchronously.
            await Task.WhenAll(
                          CleanupAsync(() => _sender.DisconnectAsync(sessionId), sessionId),
                          CleanupAsync(() => _dispatcher.DisconnectAsync(sessionId), sessionId)
                      )
                      .ConfigureAwait(false);
        }
        finally
        {
            lock (_cleanupGate)
            {
                _cleanups.Remove(completion.Task);
                completion.TrySetResult();
            }
        }
    }

    private async Task CleanupAsync(Func<Task> cleanup, long sessionId)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Packet cleanup failed for session {SessionId}", sessionId);
        }
    }

    private void TcpServerOnOnClientConnect(object? sender, TcpClientEventArgs e)
    {
        _sessionService.GetOrCreate(e.Client);
        _logger.Information(
            "Client connected from {Address} with session ID {SessionId}",
            e.Client.RemoteEndPoint,
            e.Client.SessionId
        );
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
            try
            {
                await StopListenersAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                _logger.Error(cleanupFailure, "TCP cleanup failed after listener startup failure");
            }

            throw;
        }
    }

    public Task StopAsync()
    {
        lock (_lifecycleGate)
        {
            // Serialize shutdown admission with the first startup so a cached stop task can
            // never be followed by newly opened listeners.
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

    private async Task StopListenersAsync()
    {
        List<Exception> failures = [];

        // Request every listener stop before awaiting any of them.
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

        // Listener stop has joined callbacks, so no new disconnect cleanup can arrive.
        await Task.WhenAll(pending).ConfigureAwait(false);

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }
}
