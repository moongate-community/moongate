using System.Collections.Concurrent;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.GameLoop;
using Moongate.Tests.Support.GameLoop;

namespace Moongate.Tests.Server.Services.GameLoop;

public sealed class GameLoopServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory, InlineData(0), InlineData(-1)]
    public void WorkItemBudget_NonPositive_RejectsInvalidConfiguration(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameLoopOptions
            {
                WorkItemBudget = TimeSpan.FromMilliseconds(milliseconds)
            }
        );
    }

    [Theory, InlineData(0, 1), InlineData(-1, 1), InlineData(1, 0), InlineData(1, -1)]
    public void Constructor_NonPositiveLimits_RejectsInvalidConfiguration(int capacity, int batch)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameLoopService(
                new GameLoopOptions
                {
                    QueueCapacity = capacity, MaxWorkItemsPerBatch = batch
                },
                new TimerWheelService(new TimerWheelOptions(), TimeProvider.System),
                TimeProvider.System
            )
        );
    }

    [Fact]
    public async Task StartAsync_ExecutesOnOneNamedDedicatedThreadAndClearsIdentityAfterStop()
    {
        using var loop = Create();
        var observations = new ConcurrentBag<(int Id, bool OnLoop, bool Pool, string? Name)>();
        var completion = loop.Completion;
        Assert.False(loop.IsOnLoopThread);
        Assert.False(completion.IsCompleted);
        await loop.StartAsync().WaitAsync(Timeout);
        for (var i = 0; i < 8; i++)
        {
            await loop.PostAsync(
                new ActionGameLoopWorkItem(() => observations.Add(
                        (
                            Environment.CurrentManagedThreadId, loop.IsOnLoopThread,
                            Thread.CurrentThread.IsThreadPoolThread, Thread.CurrentThread.Name)
                    )
                )
            );
        }

        await loop.StopAsync().WaitAsync(Timeout);

        Assert.Equal(8, observations.Count);
        Assert.Single(observations.Select(value => value.Id).Distinct());
        Assert.All(
            observations,
            value =>
            {
                Assert.True(value.OnLoop);
                Assert.False(value.Pool);
                Assert.False(string.IsNullOrWhiteSpace(value.Name));
            }
        );
        Assert.False(loop.IsOnLoopThread);
        Assert.Same(completion, loop.Completion);
        Assert.True(completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task PostAsync_MultipleBatches_PreservesAllItemsAndSingleProducerOrder()
    {
        using var loop = Create(4, 2);
        var executed = new List<int>();
        await loop.StartAsync();
        for (var index = 0; index < 35; index++)
        {
            var item = index;
            await loop.PostAsync(new ActionGameLoopWorkItem(() => executed.Add(item)));
        }

        await loop.StopAsync().WaitAsync(Timeout);

        Assert.Equal(Enumerable.Range(0, 35), executed);
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FullInbox_TryPostRejectsAndPostAsyncWaitsUntilSpaceWithoutInlineExecution()
    {
        using var loop = Create(1);
        using var blocker = new BlockingGameLoopWorkItem();
        var executed = 0;
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => executed++)));
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => executed += 100)));
        var pending = loop.PostAsync(new ActionGameLoopWorkItem(() => executed++)).AsTask();
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, executed);

        blocker.Release();
        await pending.WaitAsync(Timeout);
        await loop.StopAsync().WaitAsync(Timeout);

        Assert.Equal(2, executed);
    }

    [Fact]
    public async Task PostAsync_PreCanceledToken_DoesNotAcceptWorkEvenWhenSpaceExists()
    {
        using var loop = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var executed = false;
        await loop.StartAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop.PostAsync(
                new ActionGameLoopWorkItem(() => executed = true),
                cancellation.Token
            )
            .AsTask()
        );
        await loop.StopAsync();

        Assert.False(executed);
    }

    [Fact]
    public async Task PostAsync_CancellationWhileWaiting_DoesNotExecuteCanceledItem()
    {
        using var loop = Create(1);
        using var blocker = new BlockingGameLoopWorkItem();
        using var cancellation = new CancellationTokenSource();
        var executed = 0;
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => executed++)));
        var pending = loop.PostAsync(new ActionGameLoopWorkItem(() => executed += 100), cancellation.Token).AsTask();
        Assert.False(pending.IsCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Timeout));
        blocker.Release();
        await loop.StopAsync().WaitAsync(Timeout);

        Assert.Equal(1, executed);
    }

    [Fact]
    public async Task PostAsync_AcceptanceCompletesBeforeExecutionAndLaterCancellationDoesNotRetractIt()
    {
        using var loop = Create(1);
        using var blocker = new BlockingGameLoopWorkItem();
        using var cancellation = new CancellationTokenSource();
        var executed = false;
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);

        await loop.PostAsync(new ActionGameLoopWorkItem(() => executed = true), cancellation.Token)
            .AsTask()
            .WaitAsync(Timeout);
        cancellation.Cancel();
        Assert.False(executed);
        blocker.Release();
        await loop.StopAsync().WaitAsync(Timeout);

        Assert.True(executed);
    }

    [Fact]
    public async Task StopAsync_WaitsForActiveHandlerAndDrainsEveryAcceptedItem()
    {
        using var loop = Create(8, 1);
        using var blocker = new BlockingGameLoopWorkItem();
        var executed = 0;
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        for (var i = 0; i < 7; i++)
        {
            Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => executed++)));
        }

        var stopping = loop.StopAsync();
        Assert.False(stopping.IsCompleted);
        Assert.False(loop.Completion.IsCompleted);
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => executed += 100)));
        blocker.Release();
        await stopping.WaitAsync(Timeout);

        Assert.Equal(7, executed);
    }

    [Fact]
    public async Task StopAsync_WhenLastItemAlreadyDequeued_StillWaitsForHandler()
    {
        using var loop = Create(1);
        using var blocker = new BlockingGameLoopWorkItem();
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);

        var stopping = loop.StopAsync();
        Assert.False(stopping.IsCompleted);
        Assert.False(loop.Completion.IsCompleted);
        blocker.Release();
        await stopping.WaitAsync(Timeout);
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopAsync_ClosesPendingProducerWithoutExecutingItsUnacceptedWork()
    {
        using var loop = Create(1);
        using var blocker = new BlockingGameLoopWorkItem();
        var executed = 0;
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => executed++)));
        var pending = loop.PostAsync(new ActionGameLoopWorkItem(() => executed += 100)).AsTask();
        Assert.False(pending.IsCompleted);

        var stopping = loop.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(Timeout));
        blocker.Release();
        await stopping.WaitAsync(Timeout);

        Assert.Equal(1, executed);
    }

    [Fact]
    public async Task LifecycleEdges_RejectPostsBeforeStartAndAfterStopAndDisposal()
    {
        var loop = Create();
        var work = new ActionGameLoopWorkItem(() => { });
        try
        {
            Assert.False(loop.TryPost(work));
            await Assert.ThrowsAsync<InvalidOperationException>(() => loop.PostAsync(work).AsTask());
            await loop.StartAsync();
            await loop.StopAsync().WaitAsync(Timeout);
            Assert.False(loop.TryPost(work));
            await Assert.ThrowsAsync<InvalidOperationException>(() => loop.PostAsync(work).AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => loop.StartAsync());
        }
        finally
        {
            loop.Dispose();
        }

        Assert.False(loop.TryPost(work));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.PostAsync(work).AsTask());
        loop.Dispose();
    }

    [Fact]
    public async Task StopAsync_BeforeStart_PermanentlyClosesInstance()
    {
        using var loop = Create();
        var firstStop = loop.StopAsync();
        await firstStop.WaitAsync(Timeout);

        Assert.Same(firstStop, loop.StopAsync());
        Assert.True(loop.Completion.IsCompletedSuccessfully);
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.StartAsync());
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => { })));
    }

    [Fact]
    public async Task ConcurrentLifecycleCalls_ShareStartAndStopTasks()
    {
        using var loop = Create();
        var starts = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => new[] { loop.StartAsync() })));
        Assert.All(starts, start => Assert.Same(starts[0][0], start[0]));
        await starts[0][0].WaitAsync(Timeout);
        var stops = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => new[] { loop.StopAsync() })));
        Assert.All(stops, stop => Assert.Same(stops[0][0], stop[0]));
        await stops[0][0].WaitAsync(Timeout);
    }

    [Fact]
    public async Task LoopThread_PostAsyncStopAndDisposeRejectSelfWaitButTryPostCanEnqueue()
    {
        using var loop = Create(1);
        var checkedReentrancy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nestedExecuted = false;
        var producerExited = false;
        var observedProducerExited = false;
        await loop.StartAsync();
        Assert.True(
            loop.TryPost(
                new ActionGameLoopWorkItem(() =>
                    {
                        try
                        {
                            Assert.Throws<InvalidOperationException>(() => { _ = loop.StopAsync(); });
                            Assert.Throws<InvalidOperationException>(() => loop.Dispose());
                            Assert.Throws<InvalidOperationException>(() =>
                                {
                                    _ = loop.PostAsync(new ActionGameLoopWorkItem(() => { })).AsTask();
                                }
                            );
                            Assert.True(
                                loop.TryPost(
                                    new ActionGameLoopWorkItem(() =>
                                        {
                                            observedProducerExited = producerExited;
                                            nestedExecuted = true;
                                        }
                                    )
                                )
                            );
                            Assert.False(nestedExecuted);
                            Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => { })));
                            producerExited = true;
                            checkedReentrancy.SetResult();
                        }
                        catch (Exception exception)
                        {
                            checkedReentrancy.SetException(exception);
                        }
                    }
                )
            )
        );
        await checkedReentrancy.Task.WaitAsync(Timeout);
        await loop.StopAsync().WaitAsync(Timeout);

        Assert.True(nestedExecuted);
        Assert.True(observedProducerExited);
    }

    [Fact]
    public async Task HandlerFailure_FaultsStableCompletionWithOriginalExceptionAndAbandonsQueuedItems()
    {
        using var loop = Create(2);
        using var blocker = new BlockingGameLoopWorkItem();
        var failure = new ApplicationException("handler failed");
        var executedAfterFailure = false;
        var completion = loop.Completion;
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => throw failure)));
        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => executedAfterFailure = true)));
        var pending = loop.PostAsync(new ActionGameLoopWorkItem(() => executedAfterFailure = true)).AsTask();
        blocker.Release();

        var observed = await Assert.ThrowsAsync<ApplicationException>(() => completion.WaitAsync(Timeout));
        // Admission may win before the handler fails; either way it must not execute afterwards.
        try
        {
            await pending.WaitAsync(Timeout);
        }
        catch (InvalidOperationException)
        {
        }

        await loop.StopAsync().WaitAsync(Timeout);

        Assert.Same(failure, observed);
        Assert.Same(completion, loop.Completion);
        Assert.False(executedAfterFailure);
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.StartAsync());
    }

    [Fact]
    public async Task HandlerFailure_ClosesProducerWaitingBehindFullInbox()
    {
        using var loop = Create(1);
        using var blocker = new BlockingGameLoopWorkItem();
        var failure = new ApplicationException("fatal while producer waits");
        var executedAfterFailure = false;
        await loop.StartAsync();
        Assert.True(
            loop.TryPost(
                new ActionGameLoopWorkItem(() =>
                    {
                        blocker.Execute();
                        throw failure;
                    }
                )
            )
        );
        await blocker.Entered.WaitAsync(Timeout);
        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => executedAfterFailure = true)));
        var pending = loop.PostAsync(new ActionGameLoopWorkItem(() => executedAfterFailure = true)).AsTask();
        Assert.False(pending.IsCompleted);

        blocker.Release();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(Timeout));
        Assert.Same(failure, await Assert.ThrowsAsync<ApplicationException>(() => loop.Completion.WaitAsync(Timeout)));
        await loop.StopAsync().WaitAsync(Timeout);

        Assert.False(executedAfterFailure);
    }

    [Fact]
    public async Task StartStopAndDispose_RacingFromSameGate_AlwaysTerminateWithoutRestart()
    {
        using var loop = Create();
        var begin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starting = Task.Run(async () =>
            {
                await begin.Task;
                try
                {
                    await loop.StartAsync();
                }
                catch (InvalidOperationException)
                {
                    // Stop or disposal may permanently close the instance before start wins admission.
                }
            }
        );
        var stopping = Task.Run(async () =>
            {
                await begin.Task;
                await loop.StopAsync();
            }
        );
        var disposals = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(async () =>
                    {
                        await begin.Task;
                        loop.Dispose();
                    }
                )
            )
            .ToArray();

        begin.SetResult();
        await Task.WhenAll(disposals.Append(starting).Append(stopping)).WaitAsync(Timeout);

        Assert.True(loop.Completion.IsCompletedSuccessfully);
        Assert.False(loop.IsOnLoopThread);
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.StartAsync());
    }

    [Fact]
    public async Task ManyConcurrentProducers_ExecuteEveryAcceptedItemExactlyOnceWithoutOverlap()
    {
        using var loop = Create(3, 2);
        var executed = new ConcurrentBag<int>();
        var active = 0;
        var overlap = 0;
        await loop.StartAsync();
        var producers = Enumerable.Range(0, 12)
            .Select(producer => Task.Run(async () =>
                    {
                        for (var index = 0; index < 40; index++)
                        {
                            var id = producer * 40 + index;
                            await loop.PostAsync(
                                new ActionGameLoopWorkItem(() =>
                                    {
                                        if (Interlocked.Increment(ref active) != 1)
                                        {
                                            Interlocked.Increment(ref overlap);
                                        }

                                        executed.Add(id);
                                        Interlocked.Decrement(ref active);
                                    }
                                )
                            );
                        }
                    }
                )
            );
        await Task.WhenAll(producers).WaitAsync(Timeout);
        await loop.StopAsync().WaitAsync(Timeout);

        Assert.Equal(0, overlap);
        Assert.Equal(Enumerable.Range(0, 480), executed.Order());
    }

    [Fact]
    public async Task Dispose_RacingProducers_DrainsAcceptedItemsAndDoesNotThrowFromDisposedSignal()
    {
        using var loop = Create(4, 2);
        using var blocker = new BlockingGameLoopWorkItem();
        var accepted = new ConcurrentBag<int>();
        var executed = new ConcurrentBag<int>();
        var begin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await loop.StartAsync();
        Assert.True(loop.TryPost(blocker));
        await blocker.Entered.WaitAsync(Timeout);
        var producers = Enumerable.Range(0, 32)
            .Select(id => Task.Run(async () =>
                    {
                        await begin.Task;
                        try
                        {
                            await loop.PostAsync(new ActionGameLoopWorkItem(() => executed.Add(id)));
                            accepted.Add(id);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                )
            )
            .ToArray();
        var disposing = Task.Run(async () =>
            {
                await begin.Task;
                loop.Dispose();
            }
        );
        begin.SetResult();
        blocker.Release();
        await Task.WhenAll(producers.Append(disposing)).WaitAsync(Timeout);

        Assert.Equal(accepted.Order(), executed.Order());
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task NullWorkItems_ThrowRegardlessOfLifecycleState()
    {
        using var loop = Create();
        Assert.Throws<ArgumentNullException>(() => loop.TryPost(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => loop.PostAsync(null!).AsTask());
        await loop.StartAsync();
        Assert.Throws<ArgumentNullException>(() => loop.TryPost(null!));
        await loop.StopAsync();
        Assert.Throws<ArgumentNullException>(() => loop.TryPost(null!));
    }

    [Fact]
    public async Task StopAsync_TerminalItem_CapturesDrainedMutationsOnLoopThreadAfterTimersClose()
    {
        var timers = new TimerWheelService(new TimerWheelOptions(), TimeProvider.System);
        using var loop = new GameLoopService(new GameLoopOptions(), timers, TimeProvider.System);
        using var blocker = new BlockingGameLoopWorkItem();
        var mutations = 0;
        var captured = -1;
        var loopThread = 0;
        var finalThread = 0;
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        await loop.PostAsync(
            new ActionGameLoopWorkItem(() =>
                {
                    loopThread = Environment.CurrentManagedThreadId;
                    mutations++;
                }
            )
        );
        var stopping = loop.StopAsync(
            new ActionGameLoopWorkItem(() =>
                {
                    Assert.True(loop.IsOnLoopThread);
                    Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                            "late",
                            TimeSpan.FromSeconds(1),
                            () => { }
                        )
                    );
                    finalThread = Environment.CurrentManagedThreadId;
                    captured = mutations;
                }
            )
        );
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => mutations++)));
        blocker.Release();
        await stopping.WaitAsync(Timeout);
        Assert.Equal(1, captured);
        Assert.Equal(loopThread, finalThread);
    }

    [Fact]
    public async Task StopAsync_HandlerFault_SkipsTerminalItemAndReportsOriginalFailure()
    {
        using var loop = Create();
        using var blocker = new BlockingGameLoopWorkItem();
        var failure = new ApplicationException("fatal handler");
        var captured = false;
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        await loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        var stopping = loop.StopAsync(new ActionGameLoopWorkItem(() => captured = true));
        blocker.Release();
        Assert.Same(failure, await Assert.ThrowsAsync<ApplicationException>(() => stopping.WaitAsync(Timeout)));
        Assert.False(captured);
        await loop.StopAsync().WaitAsync(Timeout);
    }

    [Fact]
    public async Task StopAsync_NormalStopWins_RejectsMissedTerminalCaptureWithoutHanging()
    {
        using var loop = Create();
        using var blocker = new BlockingGameLoopWorkItem();
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        var stopping = loop.StopAsync();
        var captured = false;
        var terminal = Assert.ThrowsAsync<InvalidOperationException>(() =>
            loop.StopAsync(new ActionGameLoopWorkItem(() => captured = true)).WaitAsync(Timeout)
        );
        blocker.Release();
        await Task.WhenAll(stopping, terminal).WaitAsync(Timeout);
        Assert.False(captured);
    }

    [Fact]
    public async Task StopAsync_TerminalStopWins_NormalStopStillSucceedsAndTerminalRunsOnce()
    {
        using var loop = Create();
        var captures = 0;
        await loop.StartAsync();
        var item = new ActionGameLoopWorkItem(() => captures++);
        var terminal = loop.StopAsync(item);
        var ordinary = loop.StopAsync();
        await Task.WhenAll(terminal, ordinary).WaitAsync(Timeout);
        await loop.StopAsync(item).WaitAsync(Timeout);
        Assert.Equal(1, captures);
    }

    [Fact]
    public async Task StopAsync_TerminalFailure_PropagatesWhileOrdinaryCleanupRemainsSuccessful()
    {
        using var loop = Create();
        var failure = new ApplicationException("terminal failure");
        await loop.StartAsync();
        Assert.Same(
            failure,
            await Assert.ThrowsAsync<ApplicationException>(() =>
                loop.StopAsync(new ActionGameLoopWorkItem(() => throw failure)).WaitAsync(Timeout)
            )
        );
        await loop.StopAsync().WaitAsync(Timeout);
        Assert.Same(failure, await Assert.ThrowsAsync<ApplicationException>(() => loop.Completion));
    }

    private static GameLoopService Create(int capacity = 16, int batch = 4)
    {
        return new GameLoopService(
            new GameLoopOptions { QueueCapacity = capacity, MaxWorkItemsPerBatch = batch },
            new TimerWheelService(new TimerWheelOptions(), TimeProvider.System),
            TimeProvider.System
        );
    }
}
