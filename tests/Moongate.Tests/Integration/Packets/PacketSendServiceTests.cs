using Moongate.Network.Packets.General;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Integration.Packets;

public sealed class PacketSendServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DisconnectAsync_ExpectedConnection_DoesNotCloseReplacementWithSameId()
    {
        using var original = new ControlledNetworkConnection(9002);
        using var replacement = new ControlledNetworkConnection(9002);
        var connections = new ConnectionService();
        await connections.StartAsync();
        Assert.True(connections.TryRegister(original));
        var sender = new PacketSendService(connections);
        await sender.StartAsync();

        try
        {
            original.Complete();
            Assert.True(SpinWait.SpinUntil(() => connections.TryRegister(replacement), Timeout));
            await sender.DisconnectAsync(9002, original).WaitAsync(Timeout);
            Assert.True(replacement.IsConnected);
            Assert.True(sender.TrySend(9002, replacement, new PingPacket(7)));
            Assert.Equal(new byte[] { 0x73, 7 },
                await replacement.ReadSentAsync(CancellationToken.None).WaitAsync(Timeout));
        }
        finally
        {
            await sender.StopAsync().WaitAsync(Timeout);
            await connections.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task TrySend_ExpectedConnectionRejectsReusedSessionId()
    {
        using var original = new ControlledNetworkConnection(9001);
        using var replacement = new ControlledNetworkConnection(9001);
        var connections = new ConnectionService();
        await connections.StartAsync();
        Assert.True(connections.TryRegister(original));
        var sender = new PacketSendService(connections);
        await sender.StartAsync();

        try
        {
            await connections.DisconnectAsync(9001).WaitAsync(Timeout);
            Assert.True(connections.TryRegister(replacement));
            Assert.False(sender.TrySend(9001, original, new PingPacket(1)));
            Assert.True(sender.TrySend(9001, replacement, new PingPacket(2)));
            Assert.Equal(new byte[] { 0x73, 2 }, await replacement.ReadSentAsync(CancellationToken.None).WaitAsync(Timeout));
        }
        finally
        {
            await sender.StopAsync().WaitAsync(Timeout);
            await connections.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task ConnectionCompletion_EndsIdleOutboxWithoutDisconnectCallback()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware();
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        await sender.StartAsync();

        try
        {
            Assert.True(sender.TrySend(fixture.Client.SessionId, new PingPacket(1)));
            await middleware.ReadAsync();
            Assert.Equal(1, sender.ActiveOutboxCount);
            await fixture.Client.DisposeAsync().AsTask().WaitAsync(Timeout);
            Assert.True(
                SpinWait.SpinUntil(() => sender.ActiveOutboxCount == 0, Timeout),
                "The idle outbox must retire after connection completion, before sender shutdown."
            );
            Assert.False(sender.TrySend(fixture.Client.SessionId, new PingPacket(2)));
        }
        finally
        {
            await sender.StopAsync().WaitAsync(Timeout);
        }
    }

    [Theory, InlineData(0), InlineData(-1)]
    public async Task Constructor_RejectsNonpositiveCapacity(int capacity)
    {
        await using var fixture = await SessionFixture.CreateAsync();
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacketSendService(new ConnectionService(), capacity));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task SendFailure_ClosesConnectionAndIsObservedByCleanup(bool canceled)
    {
        await using var fixture = await SessionFixture.CreateAsync();
        Exception failure = canceled
                                ? new OperationCanceledException("independent send cancellation")
                                : new IOException("send failure");
        using var middleware = new ControlledSendMiddleware { Failure = failure };
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        await sender.StartAsync();
        Assert.True(sender.TrySend(fixture.Client.SessionId, new PingPacket(1)));
        await fixture.Client.Completion.WaitAsync(Timeout);
        Assert.False(sender.TrySend(fixture.Client.SessionId, new PingPacket(2)));
        var error = await Assert.ThrowsAsync<AggregateException>(() => sender.StopAsync().WaitAsync(Timeout));
        Assert.Contains(failure, error.Flatten().InnerExceptions);
    }

    [Fact]
    public async Task SlowSynchronousSend_DoesNotHoldLoopOrOtherSessions_AndStopAwaitsDrain()
    {
        await using var slow = await SessionFixture.CreateAsync();
        await using var fast = await SessionFixture.CreateAsync();
        using var slowMiddleware = new ControlledSendMiddleware(true);
        using var fastMiddleware = new ControlledSendMiddleware();
        slow.Client.AddMiddleware(slowMiddleware);
        fast.Client.AddMiddleware(fastMiddleware);
        var sessions = new SessionService(slow.Loop);
        sessions.GetOrCreate(slow.Client);
        sessions.GetOrCreate(fast.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(slow.Client, fast.Client);
        var sender = new PacketSendService(connections.Service);
        await sender.StartAsync();

        try
        {
            var loopThread = 0;
            await slow.ExecuteOnLoopAsync(
                () =>
                {
                    loopThread = Environment.CurrentManagedThreadId;
                    Assert.True(sender.TrySend(slow.Client.SessionId, new PingPacket(1)));
                }
            );
            await slowMiddleware.Entered.WaitAsync(Timeout);
            await slow.ExecuteOnLoopAsync(() => Assert.True(sender.TrySend(fast.Client.SessionId, new PingPacket(2))));
            Assert.NotEqual(loopThread, slowMiddleware.SendThreadId);
            Assert.Equal(new byte[] { 0x73, 2 }, await fastMiddleware.ReadAsync());
            var stopping = sender.StopAsync();
            Assert.False(stopping.IsCompleted);
            Assert.False(sender.TrySend(fast.Client.SessionId, new PingPacket(3)));
            slowMiddleware.Release();
            await stopping.WaitAsync(Timeout);
            Assert.True(slow.Client.Completion.IsCompleted);
            Assert.True(fast.Client.Completion.IsCompleted);
        }
        finally
        {
            slowMiddleware.Release();
            await sender.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task TrySend_EncodesOnceAtAdmissionAndPreservesFifoBytes()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware(true);
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        await sender.StartAsync();

        try
        {
            Assert.True(sender.TrySend(session.SessionId, new PingPacket(42)));
            await middleware.Entered.WaitAsync(Timeout);
            var packet = new MutableOutgoingPacket { Sequence = 43 };
            Assert.True(sender.TrySend(session.SessionId, packet));
            packet.Sequence = 99;
            Assert.Equal(1, packet.WriteCount);
            middleware.Release();
            Assert.Equal(new byte[] { 0x73, 42 }, await middleware.ReadAsync());
            Assert.Equal(new byte[] { 0x73, 43 }, await middleware.ReadAsync());
            Assert.Equal(1, packet.WriteCount);
        }
        finally
        {
            middleware.Release();
            await sender.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task TrySend_FullOutboxClosesConnectionAndCannotRecreateWhileSessionRemains()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var middleware = new ControlledSendMiddleware(true);
        fixture.Client.AddMiddleware(middleware);
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service, 1);
        await sender.StartAsync();

        try
        {
            Assert.True(sender.TrySend(session.SessionId, new PingPacket(1)));
            await middleware.Entered.WaitAsync(Timeout);
            Assert.True(sender.TrySend(session.SessionId, new PingPacket(2)));
            Assert.False(sender.TrySend(session.SessionId, new PingPacket(3)));
            Assert.False(connections.Service.TryGet(session.SessionId, out _));
            var cleanup = sender.DisconnectAsync(session.SessionId);
            Assert.False(cleanup.IsCompleted);
            Assert.True(sessions.TryGet(session.SessionId, out _));
            Assert.False(sender.TrySend(session.SessionId, new PingPacket(4)));
            middleware.Release();
            await cleanup.WaitAsync(Timeout);
            Assert.False(sender.TrySend(session.SessionId, new PingPacket(5)));
        }
        finally
        {
            middleware.Release();
            await sender.StopAsync().WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task TrySend_RejectsStoppedMissingDetachedAndDisconnectedSessions()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        await using var connections = await ConnectionRegistryFixture.CreateAsync(fixture.Client);
        var sender = new PacketSendService(connections.Service);
        Assert.False(sender.TrySend(session.SessionId, new PingPacket(1)));
        await sender.StartAsync();
        Assert.False(sender.TrySend(long.MaxValue, new PingPacket(1)));
        await sender.DisconnectAsync(session.SessionId).WaitAsync(Timeout);
        Assert.False(sender.TrySend(session.SessionId, new PingPacket(1)));
        session.NetworkSession.DetachClient();
        Assert.False(sender.TrySend(session.SessionId, new PingPacket(1)));
        await sender.StopAsync().WaitAsync(Timeout);
        Assert.False(sender.TrySend(session.SessionId, new PingPacket(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.StartAsync());
    }
}
