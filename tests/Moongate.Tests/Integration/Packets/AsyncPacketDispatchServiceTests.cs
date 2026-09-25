using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Integration.Packets;

public sealed class AsyncPacketDispatchServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task TryDispatch_AsyncHandlerDoesNotBlockGameLoopAndReservesSessionAtAdmission()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        var sender = new StubPacketSendService();
        container.RegisterInstance<IPacketSendService>(sender);
        container.RegisterAsyncPacketHandler<PingPacket, AsyncPingPacketHandler>();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var handler = container.Resolve<AsyncPingPacketHandler>();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.OnHandleAsync = async (context, _, cancellationToken) =>
                                {
                                    entered.TrySetResult(fixture.Loop.IsOnLoopThread);
                                    await release.Task.WaitAsync(cancellationToken);
                                    Assert.True(
                                        await context.RunOnGameLoopAsync(
                                            gameSession => gameSession.SetAccountId(new(42)),
                                            cancellationToken
                                        )
                                    );
                                    completed.TrySetResult();
                                };
        var dispatcher = new PacketDispatchService(
            fixture.Loop,
            sessions,
            container.Resolve<PacketHandlerRegistry>(),
            container
        );
        await dispatcher.StartAsync();

        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(2)));
        Assert.False(await entered.Task.WaitAsync(Timeout));
        await fixture.ExecuteOnLoopAsync(() => { });
        release.TrySetResult();
        await completed.Task.WaitAsync(Timeout);
        Assert.Equal(new(42), session.AccountId);
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task TryDispatch_FullGameLoopInboxRollsBackAsyncReservation()
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

        for (var i = 0; i < 16; i++)
        {
            Assert.True(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        }

        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        blocker.Release();
        await fixture.ExecuteOnLoopAsync(() => { });
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(2)));
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task HandlerFault_ReleasesSessionAndKeepsLoopAlive()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var handler = container.Resolve<AsyncPingPacketHandler>();
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.OnHandleAsync = (_, _, _) =>
                                {
                                    invoked.TrySetResult();

                                    throw new InvalidOperationException("test fault");
                                };
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();

        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await invoked.Task.WaitAsync(Timeout);
        Assert.True(SpinWait.SpinUntil(() => dispatcher.TryDispatch(session.SessionId, new PingPacket(2)), Timeout));
        Assert.False(fixture.Loop.Completion.IsCompleted);
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task StopAsync_CancelsPendingAsyncHandlerAndJoinsWorker()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<AsyncPingPacketHandler>().OnHandleAsync = async (_, _, cancellationToken) =>
                                                                    {
                                                                        entered.TrySetResult();

                                                                        try
                                                                        {
                                                                            await Task.Delay(
                                                                                System.Threading.Timeout.InfiniteTimeSpan,
                                                                                cancellationToken
                                                                            );
                                                                        }
                                                                        catch (OperationCanceledException)
                                                                        {
                                                                            canceled.TrySetResult();

                                                                            throw;
                                                                        }
                                                                    };
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await entered.Task.WaitAsync(Timeout);

        await dispatcher.StopAsync().WaitAsync(Timeout);
        await canceled.Task.WaitAsync(Timeout);
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(2)));
    }

    [Fact]
    public async Task TryDispatch_AtAsyncCapacityRejectsAdditionalSession()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<AsyncPingPacketHandler>().OnHandleAsync = async (_, _, cancellationToken) =>
                                                                        await release.Task.WaitAsync(cancellationToken);
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        var connections = Enumerable.Range(1, 65).Select(id => new ControlledNetworkConnection(10_000 + id)).ToArray();

        try
        {
            await dispatcher.StartAsync();

            for (var index = 0; index < 64; index++)
            {
                var session = sessions.GetOrCreate(connections[index]);
                Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket((byte)index)));

                if (index % 8 == 7)
                {
                    await fixture.ExecuteOnLoopAsync(() => { });
                }
            }

            var extra = sessions.GetOrCreate(connections[64]);
            Assert.False(dispatcher.TryDispatch(extra.SessionId, new PingPacket(65)));
        }
        finally
        {
            release.TrySetResult();
            await dispatcher.StopAsync().WaitAsync(Timeout);

            foreach (var connection in connections)
            {
                connection.Dispose();
            }
        }
    }

    [Fact]
    public async Task DisconnectAsync_CancelsHandlerAndRetiresSession()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<AsyncPingPacketHandler>().OnHandleAsync = async (_, _, cancellationToken) =>
                                                                    {
                                                                        entered.TrySetResult();

                                                                        try
                                                                        {
                                                                            await Task.Delay(
                                                                                System.Threading.Timeout.InfiniteTimeSpan,
                                                                                cancellationToken
                                                                            );
                                                                        }
                                                                        catch (OperationCanceledException)
                                                                        {
                                                                            canceled.TrySetResult();

                                                                            throw;
                                                                        }
                                                                    };
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await entered.Task.WaitAsync(Timeout);

        await dispatcher.DisconnectAsync(session.SessionId).WaitAsync(Timeout);
        await canceled.Task.WaitAsync(Timeout);
        Assert.False(sessions.TryGet(session.SessionId, out _));
        await dispatcher.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ConcurrentCallersWaitForSameWorker()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<AsyncPingPacketHandler>().OnHandleAsync = async (_, _, _) =>
                                                                    {
                                                                        entered.TrySetResult();
                                                                        await release.Task;
                                                                    };
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await entered.Task.WaitAsync(Timeout);

        var first = dispatcher.StopAsync();
        var second = dispatcher.StopAsync();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(Timeout);
    }

    [Fact]
    public async Task DisconnectAsync_ThrowingCancellationCallbackStillRetiresSession()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<AsyncPingPacketHandler>().OnHandleAsync = async (_, _, cancellationToken) =>
                                                                    {
                                                                        using var callback = cancellationToken.Register(
                                                                            () => throw new InvalidOperationException(
                                                                                      "cancel callback"
                                                                                  )
                                                                        );
                                                                        registered.TrySetResult();

                                                                        try
                                                                        {
                                                                            await Task.Delay(
                                                                                System.Threading.Timeout.InfiniteTimeSpan,
                                                                                cancellationToken
                                                                            );
                                                                        }
                                                                        finally
                                                                        {
                                                                            finished.TrySetResult();
                                                                        }
                                                                    };
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        await registered.Task.WaitAsync(Timeout);

        await Assert.ThrowsAnyAsync<Exception>(() => dispatcher.DisconnectAsync(session.SessionId));
        await finished.Task.WaitAsync(Timeout);
        Assert.False(sessions.TryGet(session.SessionId, out _));
        await dispatcher.StopAsync().WaitAsync(Timeout);
    }

    [Fact]
    public async Task StopAsync_ThrowingCancellationCallbackStillJoinsWorker()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var secondConnection = new ControlledNetworkConnection(91_001);
        using var container = CreateContainer();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        var secondSession = sessions.GetOrCreate(secondConnection);
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        container.Resolve<AsyncPingPacketHandler>().OnHandleAsync = async (_, packet, cancellationToken) =>
                                                                    {
                                                                        if (packet.Sequence == 2)
                                                                        {
                                                                            secondEntered.TrySetResult();
                                                                            await release.Task;
                                                                            finished.TrySetResult();

                                                                            return;
                                                                        }

                                                                        using var callback = cancellationToken.Register(
                                                                            () => throw new InvalidOperationException(
                                                                                      "cancel callback"
                                                                                  )
                                                                        );
                                                                        registered.TrySetResult();
                                                                        await Task.Delay(
                                                                            System.Threading.Timeout.InfiniteTimeSpan,
                                                                            cancellationToken
                                                                        );
                                                                    };
        var dispatcher = CreateDispatcher(fixture, sessions, container);
        await dispatcher.StartAsync();
        Assert.True(dispatcher.TryDispatch(session.SessionId, new PingPacket(1)));
        Assert.True(dispatcher.TryDispatch(secondSession.SessionId, new PingPacket(2)));
        await registered.Task.WaitAsync(Timeout);
        await secondEntered.Task.WaitAsync(Timeout);

        var stopping = dispatcher.StopAsync();

        try
        {
            Assert.False(stopping.IsCompleted);
            Assert.False(finished.Task.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => stopping.WaitAsync(Timeout));
        Assert.True(finished.Task.IsCompleted);
        Assert.False(dispatcher.TryDispatch(session.SessionId, new PingPacket(3)));
    }

    private static Container CreateContainer()
    {
        var container = new Container();
        container.RegisterInstance<IPacketSendService>(new StubPacketSendService());
        container.RegisterAsyncPacketHandler<PingPacket, AsyncPingPacketHandler>();

        return container;
    }

    private static PacketDispatchService CreateDispatcher(
        SessionFixture fixture,
        SessionService sessions,
        Container container
    )
    {
        return new(fixture.Loop, sessions, container.Resolve<PacketHandlerRegistry>(), container);
    }
}
