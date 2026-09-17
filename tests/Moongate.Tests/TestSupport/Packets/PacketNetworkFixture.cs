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
    public GameLoopService Loop { get; }
    public SessionService Sessions { get; }
    public PacketDispatchService Dispatcher { get; }
    public PacketSendService Sender { get; }
    public NetworkService Network { get; }
    public IReadOnlyList<MoongateTcpServer> Listeners => Network.Listeners;

    public PacketNetworkFixture(IReadOnlyList<MoongateTcpServer>? listeners = null, Func<long, Task>? disconnectSender = null)
    {
        Loop = new GameLoopService(new GameLoopOptions(), new TimerWheelService(new TimerWheelOptions(), TimeProvider.System), TimeProvider.System);
        Sessions = new SessionService(Loop);
        Sender = new PacketSendService(Sessions);
        _container.RegisterInstance<ISessionService>(Sessions);
        var networkSender = disconnectSender is null ? (IPacketSendService)Sender : new CallbackPacketSender(Sender, disconnectSender);
        _container.RegisterInstance<IPacketSendService>(networkSender);
        _container.RegisterPacketHandler<PingPacket, PingPacketHandler>();
        _container.RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        Dispatcher = new PacketDispatchService(Loop, Sessions, _container.Resolve<PacketHandlerRegistry>(), _container);
        _container.RegisterInstance<IPacketDispatchService>(Dispatcher);
        _container.RegisterInstance(new MoongateServerConfig { Network = new() { ListenAddress = "127.0.0.1", GamePort = 0 } });
        _container.Register<NetworkService>();
        Network = listeners is null ? _container.Resolve<NetworkService>() : new NetworkService(listeners, Sessions, Dispatcher, networkSender);
    }

    public async Task StartAsync()
    {
        await Loop.StartAsync();
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
        await Network.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await Sender.StopAsync();
        await Dispatcher.StopAsync();
        await Loop.StopAsync();
        Loop.Dispose();
        _container.Dispose();
    }
}
