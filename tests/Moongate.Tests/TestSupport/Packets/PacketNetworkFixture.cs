using System.Net;
using System.Net.Sockets;
using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Server;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Handlers.Login;

namespace Moongate.Tests.TestSupport.Packets;

internal sealed class PacketNetworkFixture : IAsyncDisposable
{
    private readonly Container _container = new();
    private readonly bool _sendRawSeedOnConnect;
    public bool AllowCleanupFailure { get; set; }
    public GameLoopService Loop { get; }
    public SessionService Sessions { get; }
    public ConnectionService Connections { get; }
    public PacketDispatchService Dispatcher { get; }
    public PacketSendService Sender { get; }
    public NetworkService Network { get; }
    public GameServerService Game { get; }
    public IReadOnlyList<MoongateTcpServer> Listeners => Network.Listeners;

    public PacketNetworkFixture(
        IReadOnlyList<MoongateTcpServer>? listeners = null,
        Func<long, Task>? disconnectSender = null
    )
    {
        _sendRawSeedOnConnect = listeners is null;
        Loop = new(
            new(),
            new(new(), TimeProvider.System),
            TimeProvider.System
        );
        Sessions = new(Loop);
        Connections = new();
        Sender = new(Connections);
        _container.RegisterInstance<IConnectionService>(Connections);
        _container.RegisterInstance<ISessionService>(Sessions);
        var networkSender = disconnectSender is null
            ? (IPacketSendService)Sender
            : new CallbackPacketSender(Sender, disconnectSender);
        _container.RegisterInstance(networkSender);
        _container.RegisterPacketHandler<PingPacket, PingPacketHandler>();
        _container.RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        Dispatcher = new(Loop, Sessions, _container.Resolve<PacketHandlerRegistry>(), _container);
        _container.RegisterInstance<IPacketDispatchService>(Dispatcher);
        var config = new MoongateServerConfig { Network = new() { ListenAddress = "127.0.0.1", GamePort = 0 } };
        _container.RegisterInstance(config);
        Network = listeners is null
            ? new(UoNetworkOptionsFactory.CreateGame(config), Connections)
            : new NetworkService(listeners, Connections);
        Game = new(Network, Connections, Sessions, Dispatcher, networkSender);
    }

    public async Task<TcpClient> ConnectAsync()
    {
        var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, Listeners[0].Port);

        if (_sendRawSeedOnConnect)
        {
            await peer.GetStream().WriteAsync(new byte[] { 0x12, 0x34, 0x56, 0x78 });
        }

        return peer;
    }

    public async Task StartAsync()
    {
        await Loop.StartAsync();
        await Connections.StartAsync();
        await Sender.StartAsync();
        await Dispatcher.StartAsync();
        await Game.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        List<Exception> failures = [];

        foreach (var service in new IMoongateStartupService[] { Game, Dispatcher, Sender, Connections, Loop })
        {
            try
            {
                await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        Loop.Dispose();
        _container.Dispose();

        if (!AllowCleanupFailure && failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }
}
