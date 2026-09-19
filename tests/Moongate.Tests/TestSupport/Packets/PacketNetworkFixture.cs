using System.Net;
using System.Net.Sockets;
using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Server;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Data.Config;
using Moongate.Server.Handlers.General;
using Moongate.Server.Handlers.Login;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Timing;

namespace Moongate.Tests.TestSupport.Packets;

internal sealed class PacketNetworkFixture : IAsyncDisposable
{
    private readonly Container _container = new();
    public bool AllowCleanupFailure { get; set; }
    public GameLoopService Loop { get; }
    public SessionService Sessions { get; }
    public ConnectionService Connections { get; }
    public PacketDispatchService Dispatcher { get; }
    public PacketSendService Sender { get; }
    public NetworkService Network { get; }
    public IReadOnlyList<MoongateTcpServer> Listeners => Network.Listeners;

    public PacketNetworkFixture(IReadOnlyList<MoongateTcpServer>? listeners = null, Func<long, Task>? disconnectSender = null)
    {
        Loop = new GameLoopService(new GameLoopOptions(), new TimerWheelService(new TimerWheelOptions(), TimeProvider.System), TimeProvider.System);
        Sessions = new SessionService(Loop);
        Connections = new ConnectionService();
        Sender = new PacketSendService(Connections);
        _container.RegisterInstance<IConnectionService>(Connections);
        _container.RegisterInstance<ISessionService>(Sessions);
        var networkSender = disconnectSender is null ? (IPacketSendService)Sender : new CallbackPacketSender(Sender, disconnectSender);
        _container.RegisterInstance<IPacketSendService>(networkSender);
        _container.RegisterPacketHandler<PingPacket, PingPacketHandler>();
        _container.RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        Dispatcher = new PacketDispatchService(Loop, Sessions, _container.Resolve<PacketHandlerRegistry>(), _container);
        _container.RegisterInstance<IPacketDispatchService>(Dispatcher);
        _container.RegisterInstance(new MoongateServerConfig { Network = new() { ListenAddress = "127.0.0.1", GamePort = 0 } });
        _container.Register<NetworkService>();
        Network = listeners is null ? _container.Resolve<NetworkService>() : new NetworkService(listeners, Sessions, Dispatcher, networkSender, Connections);
    }

    public async Task StartAsync()
    {
        await Loop.StartAsync();
        await Connections.StartAsync();
        await Sender.StartAsync();
        await Dispatcher.StartAsync();
        await Network.StartAsync();
    }

    public async Task<TcpClient> ConnectAsync()
    {
        var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, Listeners[0].Port);
        return peer;
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception> failures = [];
        foreach (var service in new IMoongateStartupService[] { Network, Dispatcher, Sender, Connections, Loop })
        {
            try { await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception exception) { failures.Add(exception); }
        }
        Loop.Dispose();
        _container.Dispose();
        if (!AllowCleanupFailure && failures.Count > 0) { throw new AggregateException(failures); }
    }
}
