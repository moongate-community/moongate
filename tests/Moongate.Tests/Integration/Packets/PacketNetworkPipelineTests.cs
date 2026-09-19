using DryIoc;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Handlers.General;
using Moongate.Server.Handlers.Login;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Timing;
using System.Net;
using System.Net.Sockets;
using Moongate.Network.Server;
using Moongate.Network.Data;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Services.Network.Framing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Integration.Packets;

public sealed class PacketNetworkPipelineTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task FragmentedPing_ProducesExactlyOneReply()
    {
        await using var fixture = new PacketNetworkFixture();
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        await peer.GetStream().WriteAsync(new byte[] { 0x73 });
        using (var pause = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { _ = await peer.GetStream().ReadAsync(new byte[1], pause.Token); });
        }
        await peer.GetStream().WriteAsync(new byte[] { 42 });
        Assert.Equal(new byte[] { 0x73, 42 }, await ReadAsync(peer, 2));
        await fixture.Network.StopAsync().WaitAsync(Timeout);
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
        Assert.Equal(0, fixture.Sessions.Count);
    }

    [Fact]
    public async Task CoalescedPings_ReplyInWireOrder()
    {
        await using var fixture = new PacketNetworkFixture();
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        await peer.GetStream().WriteAsync(new byte[] { 0x73, 7, 0x73, 8, 0x73, 9 });
        Assert.Equal(new byte[] { 0x73, 7, 0x73, 8, 0x73, 9 }, await ReadAsync(peer, 6));
    }

    [Fact]
    public async Task VersionThenPing_RecordsVersionBeforeReply()
    {
        await using var fixture = new PacketNetworkFixture();
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        await peer.GetStream().WriteAsync(new byte[] { 0xBD, 0, 7, (byte)'7', (byte)'.', (byte)'0', 0, 0x73, 4 });
        Assert.Equal(new byte[] { 0x73, 4 }, await ReadAsync(peer, 2));
        Assert.Equal("7.0", Assert.Single(fixture.Sessions.GetAll()).NetworkSession.ClientVersion);
    }

    [Theory,
     InlineData("BD000420"), InlineData("BD00052000"),
     InlineData("BD000920090A0B0C0D"), InlineData("BD000A20090A0B0C0D00")]
    public async Task WhitespaceVersion_ClosesOnlyOffenderAndKeepsLoopServingOtherClients(string hex)
    {
        await using var fixture = new PacketNetworkFixture();
        await fixture.StartAsync();
        using var healthy = await fixture.ConnectAsync();
        await healthy.GetStream().WriteAsync(new byte[] { 0x73, 41 });
        Assert.Equal(new byte[] { 0x73, 41 }, await ReadAsync(healthy, 2));

        using var offender = await fixture.ConnectAsync();
        await offender.GetStream().WriteAsync(Convert.FromHexString(hex));
        using var deadline = new CancellationTokenSource(Timeout);
        var closed = offender.GetStream().ReadAsync(new byte[1], deadline.Token).AsTask();
        await Task.WhenAny(closed, fixture.Loop.Completion).WaitAsync(Timeout);
        Assert.False(fixture.Loop.Completion.IsCompleted);
        Assert.Equal(0, await closed.WaitAsync(Timeout));

        await healthy.GetStream().WriteAsync(new byte[] { 0x73, 42 });
        Assert.Equal(new byte[] { 0x73, 42 }, await ReadAsync(healthy, 2));
        Assert.False(fixture.Loop.Completion.IsCompleted);
    }

    [Theory, InlineData(new byte[] { 0xFF }), InlineData(new byte[] { 0xBD, 0, 3 }), InlineData(new byte[] { 0xBD, 0, 4, 0 }), InlineData(new byte[] { 0xA0, 0, 1 })]
    public async Task UnknownMalformedOrUnhandledPacket_ClosesConnection(byte[] bytes)
    {
        await using var fixture = new PacketNetworkFixture();
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        await peer.GetStream().WriteAsync(bytes);
        Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
        await fixture.Network.StopAsync().WaitAsync(Timeout);
        Assert.Equal(0, fixture.Sessions.Count);
    }

    [Fact]
    public async Task BindFailure_PropagatesAndClosesEarlierListeners()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        await using var fixture = new PacketNetworkFixture([new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0)), new MoongateTcpServer((IPEndPoint)occupied.LocalEndpoint)]);
        await Assert.ThrowsAsync<SocketException>(() => fixture.StartAsync());
        Assert.Equal(0, fixture.Listeners[0].Port);
    }

    [Fact]
    public async Task Bootstrap_RegistersDeferredPluginHandlerAndStopsNetworkBeforePacketDependenciesAndLoop()
    {
        using var container = new Container();
        container.RegisterInstance(new GameLoopOptions());
        container.RegisterInstance(new TimerWheelOptions());
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterInstance(new MoongateServerConfig { Network = new() { ListenAddress = "127.0.0.1", GamePort = 0 } });
        container.RegisterMoongateService<TimerWheelService>(-900);
        container.RegisterMoongateService<IGameLoopService, GameLoopService>(-800);
        container.RegisterMoongateService<ISessionService, SessionService>();
        container.RegisterInstance<IPluginLoaderService>(new DeferredPacketPluginLoader(container));
        container.RegisterPacketHandler<PingPacket, PingPacketHandler>()
                 .RegisterPacketHandler<ClientVersionPacket, ClientVersionPacketHandler>();
        PacketPipelineRegistration.Register(container);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        try
        {
            await bootstrap.StartAsync();
            var network = (NetworkService)container.Resolve<INetworkService>();
            var dependency = container.Resolve<PacketPluginDependency>();
            Assert.Same(container.Resolve<IPacketSendService>(), container.Resolve<IPacketSendService>());
            Assert.Same(container.Resolve<IPacketDispatchService>(), container.Resolve<IPacketDispatchService>());
            using var peer = new TcpClient();
            await peer.ConnectAsync(IPAddress.Loopback, network.Listeners[0].Port);
            await peer.GetStream().WriteAsync(new byte[] { 0xA0, 0, 17, 0x73, 18 });
            Assert.Equal(new byte[] { 0x73, 17, 0x73, 18 }, await ReadAsync(peer, 4));
            await bootstrap.StopAsync().WaitAsync(Timeout);
            Assert.True(dependency.Stopped);
            Assert.Equal(0, await peer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
        }
        finally
        {
            await bootstrap.StopAsync().WaitAsync(Timeout);
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Stop_WaitsForConnectionCleanupAndStillRetiresSessionIfSenderFails(bool failSender)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new PacketNetworkFixture(disconnectSender: _ =>
        {
            entered.TrySetResult();
            if (failSender) throw new IOException("Controlled sender cleanup failure.");
            return release.Task;
        });
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        await peer.GetStream().WriteAsync(new byte[] { 0x73, 1 });
        await ReadAsync(peer, 2);
        var session = Assert.Single(fixture.Sessions.GetAll());
        var client = session.NetworkSession.Client!;
        try
        {
            var stopping = fixture.Network.StopAsync();
            await entered.Task.WaitAsync(Timeout);
            await client.Completion.WaitAsync(Timeout);
            if (!failSender) Assert.False(stopping.IsCompleted);
            release.TrySetResult();
            await stopping.WaitAsync(Timeout);
            Assert.Empty(fixture.Sessions.GetAll());
            Assert.Null(session.NetworkSession.Client);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Stop_RetiresSessionAfterLoopWorkCompletesOrFaults(bool faultLoop)
    {
        await using var fixture = new PacketNetworkFixture();
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        await peer.GetStream().WriteAsync(new byte[] { 0x73, 1 });
        await ReadAsync(peer, 2);
        var session = Assert.Single(fixture.Sessions.GetAll());
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        if (faultLoop) Assert.True(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => throw new IOException("fatal loop failure"))));
        fixture.AllowCleanupFailure = faultLoop;
        var stopping = fixture.Network.StopAsync();
        Assert.False(stopping.IsCompleted);
        Assert.Same(session, Assert.Single(fixture.Sessions.GetAll()));
        blocker.Release();
        await stopping.WaitAsync(Timeout);
        Assert.Empty(fixture.Sessions.GetAll());
        Assert.Null(session.NetworkSession.Client);
        if (faultLoop) await Assert.ThrowsAsync<IOException>(() => fixture.Loop.Completion.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Stop_ClosesAllListenersAndDrainsSessionsWhenOneListenerCleanupFails()
    {
        var first = new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0),
            connectionPipelineFactory: () => new ConnectionPipeline(middlewares: [new FailingCleanupMiddleware()], framer: new UoPacketFramer(PacketRegistry.Default)));
        var second = new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0), framer: new UoPacketFramer(PacketRegistry.Default));
        await using var fixture = new PacketNetworkFixture([first, second]) { AllowCleanupFailure = true };
        await fixture.StartAsync();
        using var peer = await fixture.ConnectAsync();
        using var otherPeer = new TcpClient();
        await otherPeer.ConnectAsync(IPAddress.Loopback, second.Port);
        await peer.GetStream().WriteAsync(new byte[] { 0x73, 1 });
        await otherPeer.GetStream().WriteAsync(new byte[] { 0x73, 2 });
        await ReadAsync(peer, 2);
        await ReadAsync(otherPeer, 2);
        var failure = await Assert.ThrowsAsync<AggregateException>(() => fixture.Network.StopAsync().WaitAsync(Timeout));
        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception is IOException);
        Assert.Empty(fixture.Sessions.GetAll());
        Assert.Equal(0, first.Port);
        Assert.Equal(0, second.Port);
        Assert.Equal(0, await otherPeer.GetStream().ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
    }

    [Fact]
    public async Task BindFailure_PreservesOriginalErrorWhenEarlierListenerCleanupAlsoFails()
    {
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var first = new MoongateTcpServer(new IPEndPoint(IPAddress.Loopback, 0),
            connectionPipelineFactory: () => new ConnectionPipeline(middlewares: [new FailingCleanupMiddleware()]));
        await using var fixture = new PacketNetworkFixture([first, new MoongateTcpServer((IPEndPoint)occupied.LocalEndpoint)]) { AllowCleanupFailure = true };
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.OnClientConnect += (_, _) => connected.TrySetResult();
        await fixture.Loop.StartAsync();
        await fixture.Connections.StartAsync();
        await fixture.Sender.StartAsync();
        await fixture.Dispatcher.StartAsync();
        await first.StartAsync(default);
        using var peer = await fixture.ConnectAsync();
        await connected.Task.WaitAsync(Timeout);
        await peer.GetStream().WriteAsync(new byte[] { 0x73, 1 });
        await ReadAsync(peer, 2);
        await Assert.ThrowsAsync<SocketException>(() => fixture.StartAsync().WaitAsync(Timeout));
        Assert.Equal(0, first.Port);
        Assert.Empty(fixture.Sessions.GetAll());
    }

    [Fact]
    public async Task StopBeforeStart_RejectsStartupAndRepeatedStopLeavesListenersClosed()
    {
        await using var fixture = new PacketNetworkFixture();
        try
        {
            await fixture.Network.StopAsync().WaitAsync(Timeout);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Network.StartAsync());
            await fixture.Network.StopAsync().WaitAsync(Timeout);
            Assert.All(fixture.Listeners, listener => Assert.Equal(0, listener.Port));
        }
        finally
        {
            // Keep the regression test leak-free even when startup incorrectly opens a listener.
            foreach (var listener in fixture.Listeners)
            {
                await listener.StopAsync(default).WaitAsync(Timeout);
            }
        }
    }

    private static async Task<byte[]> ReadAsync(TcpClient peer, int count)
    {
        var bytes = new byte[count];
        using var deadline = new CancellationTokenSource(Timeout);
        await peer.GetStream().ReadExactlyAsync(bytes, deadline.Token);
        return bytes;
    }
}
