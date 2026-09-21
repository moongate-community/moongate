using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.TestSupport.Game;

internal sealed class GameCoordinatorFixture : IAsyncDisposable
{
    private readonly Container _container = new();

    public GameLoopService Loop { get; }
    public ConnectionService Connections { get; } = new();
    public NetworkServiceStub Network { get; }
    public SessionService Sessions { get; }
    public PacketSendService Sender { get; }
    public PacketDispatchService Dispatcher { get; }
    public GameServerService Game { get; }
    public RecordingPacketHandler Handler => _container.Resolve<RecordingPacketHandler>();
    public bool AllowCleanupFailure { get; set; }

    public GameCoordinatorFixture(int capacity = 16, Func<long, Task>? disconnect = null)
    {
        Loop = new(
            new() { QueueCapacity = capacity },
            new(new(), TimeProvider.System),
            TimeProvider.System
        );
        Network = new(Connections);
        Sessions = new(Loop);
        Sender = new(Connections);
        _container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>();
        _container.RegisterPacketHandler<ClientVersionPacket, RecordingPacketHandler>();
        Dispatcher = new(Loop, Sessions, _container.Resolve<PacketHandlerRegistry>(), _container);
        Game = new(
            Network,
            Connections,
            Sessions,
            Dispatcher,
            disconnect is null ? Sender : new CallbackPacketSender(Sender, disconnect)
        );
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

    public async Task StartAsync()
    {
        await StartDependenciesAsync();
        await Game.StartAsync();
    }

    public async Task StartDependenciesAsync()
    {
        await Loop.StartAsync();
        await Connections.StartAsync();
        await Sender.StartAsync();
        await Dispatcher.StartAsync();
    }
}
