using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Game;
using Moongate.Tests.TestSupport.Network;

namespace Moongate.Tests.Integration.Game;

public sealed class GameServerServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task BorrowedFrame_IsDecodedBeforeReturningToTransport()
    {
        await using var fixture = new GameCoordinatorFixture();
        await fixture.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        fixture.Network.Accept(connection);
        var received = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.OnPing = (_, sequence) => received.TrySetResult(sequence);
        using var blocker = new BlockingGameLoopWorkItem();
        fixture.Loop.TryPost(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        var version = new byte[] { 0xBD, 0, 7, (byte)'7', (byte)'.', (byte)'0', 0 };
        var ping = new byte[] { 0x73, 42 };
        fixture.Network.Receive(connection, version);
        fixture.Network.Receive(connection, ping);
        Array.Fill<byte>(version, 0xFF);
        Array.Fill<byte>(ping, 0xFF);
        Assert.False(received.Task.IsCompleted);
        blocker.Release();
        Assert.Equal(42, await received.Task.WaitAsync(Timeout));
        Assert.Equal("7.0", fixture.Handler.Version);
    }

    [Fact]
    public async Task CloseInsideHandler_RetiresWithoutWaitingForTheExecutingLoop()
    {
        await using var fixture = new GameCoordinatorFixture();
        await fixture.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        fixture.Network.Accept(connection);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Handler.OnPing = (session, _) =>
                                 {
                                     fixture.Network.Close(connection);
                                     Assert.Null(session.NetworkSession.Client);
                                     returned.TrySetResult();
                                 };
        fixture.Network.Receive(connection, new byte[] { 0x73, 1 });
        await returned.Task.WaitAsync(Timeout);
        await fixture.Game.StopAsync().WaitAsync(Timeout);
        Assert.Empty(fixture.Sessions.GetAll());
    }

    [Fact]
    public async Task FailedStartAfterAccept_PreservesOriginalFailureAndRetiresSessionDespiteCleanupFailure()
    {
        await using var fixture = new GameCoordinatorFixture(disconnect: _ => throw new IOException("sender cleanup"))
            { AllowCleanupFailure = true };
        await fixture.StartDependenciesAsync();
        using var connection = new ControlledNetworkConnection(1);
        var startupFailure = new InvalidOperationException("startup after admission");
        fixture.Network.OnStart = () =>
                                  {
                                      fixture.Network.Accept(connection);

                                      return Task.FromException(startupFailure);
                                  };
        Assert.Same(startupFailure, await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Game.StartAsync()));
        Assert.Empty(fixture.Sessions.GetAll());
        Assert.Equal(0, fixture.Connections.Count);
        Assert.Equal(0, fixture.Network.SubscriberCount);
        await Assert.ThrowsAsync<AggregateException>(() => fixture.Game.StopAsync());
    }

    [Fact]
    public async Task FullInbox_DisconnectCallbackReturnsBeforeRetirement_AndStopJoinsIt()
    {
        await using var fixture = new GameCoordinatorFixture(1);
        await fixture.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        fixture.Network.Accept(connection);
        var session = Assert.Single(fixture.Sessions.GetAll());
        using var blocker = new BlockingGameLoopWorkItem();
        fixture.Loop.TryPost(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        fixture.Network.Close(connection);
        Assert.True(connection.Completion.IsCompleted);
        var stopping = fixture.Game.StopAsync();
        Assert.False(stopping.IsCompleted);
        Assert.Same(session, Assert.Single(fixture.Sessions.GetAll()));
        blocker.Release();
        await stopping.WaitAsync(Timeout);
        Assert.Empty(fixture.Sessions.GetAll());
        Assert.Null(session.NetworkSession.Client);
        Assert.Equal(0, fixture.Connections.Count);
        Assert.Equal(0, fixture.Sender.ActiveOutboxCount);
        Assert.Equal(0, fixture.Network.SubscriberCount);
    }

    [Fact]
    public async Task RemoteCloseAndConcurrentStop_ShareCleanupWhileDependenciesRemainAvailable()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new GameCoordinatorFixture();
        await fixture.StartAsync();
        using var connection = new ControlledNetworkConnection(1);
        fixture.Network.Accept(connection);
        fixture.Network.OnStop = () =>
                                 {
                                     entered.TrySetResult();

                                     return release.Task;
                                 };

        try
        {
            var first = fixture.Game.StopAsync();
            await entered.Task.WaitAsync(Timeout);
            fixture.Network.Close(connection);
            Assert.Same(first, fixture.Game.StopAsync());
            Assert.False(first.IsCompleted);
            Assert.False(fixture.Loop.Completion.IsCompleted);
            release.TrySetResult();
            await first.WaitAsync(Timeout);
            Assert.Empty(fixture.Sessions.GetAll());
            Assert.Equal(0, fixture.Connections.Count);
        }
        finally
        {
            release.TrySetResult();
        }
    }
}
