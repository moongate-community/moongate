using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Sessions;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Integration.Packets;

public sealed class PacketDispatchServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task TryDispatch_ExecutesTypedHandlerInFifoOrderOnLoopThread()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        var observed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sequences = new List<byte>();
        container.Resolve<RecordingPacketHandler>().OnPing = (actualSession, sequence) =>
        {
            Assert.Same(session, actualSession);
            Assert.True(fixture.Loop.IsOnLoopThread);
            sequences.Add(sequence);
            if (sequences.Count == 2) observed.SetResult(Environment.CurrentManagedThreadId);
        };
        await dispatcher.StartAsync();
        var loopThreadId = 0;
        await fixture.ExecuteOnLoopAsync(() => loopThreadId = Environment.CurrentManagedThreadId);
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(42)));
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(43)));
        Assert.Equal(loopThreadId, await observed.Task.WaitAsync(Timeout));
        Assert.Equal(new byte[] { 42, 43 }, sequences);
    }

    [Fact]
    public async Task TryDispatch_RejectsUnavailableAdmissionAndSkipsDisconnectedQueuedWork()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        var invoked = 0;
        container.Resolve<RecordingPacketHandler>().OnPing = (_, _) => Interlocked.Increment(ref invoked);
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await dispatcher.StartAsync();
        Assert.False(dispatcher.TryDispatch(long.MaxValue, new PingPacket(1)));
        Assert.False(dispatcher.TryDispatch(session.SessionId, new ClientVersionPacket("7")));
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        session.NetworkSession.DetachClient();
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(2)));
        blocker.Release();
        await fixture.ExecuteOnLoopAsync(() => { });
        Assert.Equal(0, invoked);
        await dispatcher.StopAsync();
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
    }

    [Fact]
    public async Task TryDispatch_FullQueueRejectsWithoutInlineExecutionAndDisconnectWaitsForRetirement()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        var invoked = 0;
        container.Resolve<RecordingPacketHandler>().OnPing = (_, _) => Interlocked.Increment(ref invoked);
        await dispatcher.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        for (var i = 0; i < 16; i++) Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket((byte)i)));
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(99)));
        Assert.Equal(0, invoked);
        var cleanup = dispatcher.DisconnectAsync(session.SessionId);
        Assert.False(cleanup.IsCompleted);
        Assert.Same(cleanup, dispatcher.DisconnectAsync(session.SessionId));
        Assert.Same(fixture.Client, session.NetworkSession.Client);
        blocker.Release();
        await cleanup.WaitAsync(Timeout);
        Assert.Equal(16, invoked);
        Assert.Equal(0, sessions.Count);
        Assert.Null(session.NetworkSession.Client);
        Assert.Equal(NetworkSessionState.Disconnected, session.NetworkSession.State);
        Assert.True(fixture.Client.IsConnected);
        await dispatcher.DisconnectAsync(session.SessionId).WaitAsync(Timeout);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Disconnect_LoopFaultRetiresBothQueuedAndWaitingCleanup(bool fullQueue)
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        var failure = new InvalidOperationException("fatal packet handler");
        container.Resolve<RecordingPacketHandler>().OnPing = (_, _) => throw failure;
        await dispatcher.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        if (fullQueue)
        {
            for (var i = 1; i < 16; i++) Assert.True(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        }
        var cleanup = dispatcher.DisconnectAsync(session.SessionId);
        Assert.False(cleanup.IsCompleted);
        blocker.Release();
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Loop.Completion.WaitAsync(Timeout)));
        await cleanup.WaitAsync(Timeout);
        Assert.Equal(0, sessions.Count);
        Assert.Null(session.NetworkSession.Client);
        Assert.True(fixture.Client.IsConnected);
        await dispatcher.DisconnectAsync(session.SessionId).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Disconnect_BeforeDispatcherAndLoopStartCompletesWithoutWaitingForLoop()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var neverStartedLoop = new GameLoopService(new GameLoopOptions(),
            new TimerWheelService(new TimerWheelOptions(), TimeProvider.System), TimeProvider.System);
        using var container = CreateContainer();
        var sessions = new SessionService(neverStartedLoop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = new PacketDispatchService(neverStartedLoop, sessions, container.Resolve<PacketHandlerRegistry>(), container);
        await dispatcher.DisconnectAsync(session.SessionId).WaitAsync(Timeout);
        Assert.False(neverStartedLoop.Completion.IsCompleted);
        Assert.False(sessions.TryGet(session.SessionId, out _));
        Assert.Null(session.NetworkSession.Client);
    }

    [Fact]
    public async Task Stop_ClosesAdmissionButCleanupWaitsForLoopShutdownBeforeRetiring()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        await dispatcher.StopAsync();
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        var stopping = fixture.Loop.StopAsync();
        var cleanup = dispatcher.DisconnectAsync(session.SessionId);
        Assert.False(cleanup.IsCompleted);
        Assert.Same(fixture.Client, session.NetworkSession.Client);
        blocker.Release();
        await stopping.WaitAsync(Timeout);
        await cleanup.WaitAsync(Timeout);
        Assert.False(sessions.TryGet(session.SessionId, out _));
        Assert.Null(session.NetworkSession.Client);
    }

    [Fact]
    public async Task TryDispatch_RechecksTransportConnectionWhenQueuedWorkExecutes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        var invoked = 0;
        container.Resolve<RecordingPacketHandler>().OnPing = (_, _) => Interlocked.Increment(ref invoked);
        await dispatcher.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        Assert.True(fixture.Loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await fixture.Client.DisposeAsync().AsTask().WaitAsync(Timeout);
        blocker.Release();
        await fixture.ExecuteOnLoopAsync(() => { });
        Assert.Equal(0, invoked);
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(2)));
    }

    [Fact]
    public async Task Disconnect_OnLoopThreadRetiresWithoutWaitingForItsOwnInbox()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        await fixture.ExecuteOnLoopAsync(() => Assert.True(dispatcher.DisconnectAsync(session.SessionId).IsCompletedSuccessfully));
        Assert.False(sessions.TryGet(session.SessionId, out _));
    }

    private static Container CreateContainer()
    {
        var container = new Container();
        container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>();
        return container;
    }

    private static PacketDispatchService CreateDispatcher(SessionFixture fixture, SessionService sessions, Container container)
    {
        return new PacketDispatchService(fixture.Loop, sessions, container.Resolve<PacketHandlerRegistry>(), container);
    }
}
