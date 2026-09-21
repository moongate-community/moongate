using DryIoc;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Timing;

namespace Moongate.Tests.Integration.GameLoop;

public sealed class GameLoopTimingTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task DueTimerBacklog_AllowsCommandsBetweenBoundedBatches()
    {
        var clock = new ManualTimeProvider();
        var timers = CreateTimers(clock, 2);
        using var loop = CreateLoop(timers, clock);
        using var blocker = new BlockingGameLoopWorkItem();
        var commandObserved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allTimers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = 0;
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(TestTimeout);

        for (var i = 0; i < 8; i++)
        {
            timers.RegisterTimer(
                "due",
                TimeSpan.FromMilliseconds(1),
                () =>
                {
                    callbacks++;

                    if (callbacks == 1)
                    {
                        Assert.True(loop.TryPost(new ActionGameLoopWorkItem(() => commandObserved.SetResult(callbacks))));
                    }

                    if (callbacks == 8)
                    {
                        allTimers.SetResult();
                    }
                }
            );
        }

        clock.Advance(TimeSpan.FromMilliseconds(1));

        blocker.Release();
        var callbackCountAtCommand = await commandObserved.Task.WaitAsync(TestTimeout);
        await allTimers.Task.WaitAsync(TestTimeout);
        await loop.StopAsync();

        Assert.InRange(callbackCountAtCommand, 1, 2);
        Assert.Equal(8, callbacks);
    }

    [Fact]
    public async Task FullCommandQueue_ChecksDueTimersBetweenBoundedBatches()
    {
        var clock = new ManualTimeProvider();
        var timers = CreateTimers(clock);
        using var loop = CreateLoop(timers, clock, 2);
        using var blocker = new BlockingGameLoopWorkItem();
        var observed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = 0;
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(TestTimeout);
        timers.RegisterTimer("due", TimeSpan.FromMilliseconds(1), () => observed.SetResult(commands));

        for (var i = 0; i < 8; i++)
        {
            Assert.True(
                loop.TryPost(
                    new ActionGameLoopWorkItem(
                        () =>
                        {
                            commands++;
                            clock.Advance(TimeSpan.FromMilliseconds(1));
                        }
                    )
                )
            );
        }

        blocker.Release();
        var commandCountAtTimer = await observed.Task.WaitAsync(TestTimeout);
        await loop.StopAsync();

        Assert.InRange(commandCountAtTimer, 1, 2);
        Assert.Equal(8, commands);
    }

    [Fact]
    public async Task IdleLoop_NewEarlierTimer_WakesAndWaitsForRealDeadline()
    {
        var timers = CreateTimers(TimeProvider.System);
        using var loop = CreateLoop(timers, TimeProvider.System);
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var threadObserved = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        await loop.StartAsync();
        timers.RegisterTimer("far", TimeSpan.FromDays(100), () => throw new InvalidOperationException("Early far timer"));
        await loop.PostAsync(new ActionGameLoopWorkItem(() => threadObserved.SetResult(Thread.CurrentThread)));
        var thread = await threadObserved.Task.WaitAsync(TestTimeout);
        Assert.True(
            SpinWait.SpinUntil(
                () => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TestTimeout
            )
        );
        timers.RegisterTimer("near", TimeSpan.FromMilliseconds(20), () => fired.SetResult());

        await fired.Task.WaitAsync(TestTimeout);
        await loop.StopAsync();
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ImmediateCommand_DoesNotWaitForDistantTimer()
    {
        var timers = CreateTimers(TimeProvider.System);
        using var loop = CreateLoop(timers, TimeProvider.System);
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        timers.RegisterTimer("far", TimeSpan.FromDays(100), () => throw new InvalidOperationException("Early far timer"));
        await loop.StartAsync();

        await loop.PostAsync(new ActionGameLoopWorkItem(() => executed.SetResult()));

        await executed.Task.WaitAsync(TestTimeout);
        await loop.StopAsync();
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopAsync_CancelsFutureAndRepeatingTimersAndDrainsCommands()
    {
        var clock = new ManualTimeProvider();
        var timers = CreateTimers(clock);
        using var loop = CreateLoop(timers, clock);
        using var blocker = new BlockingGameLoopWorkItem();
        var callbacks = 0;
        var commands = 0;
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(TestTimeout);
        timers.RegisterTimer("repeat", TimeSpan.FromMilliseconds(1), () => callbacks++, repeat: true);
        timers.RegisterTimer("future", TimeSpan.FromDays(1), () => callbacks++);
        await loop.PostAsync(new ActionGameLoopWorkItem(() => commands++));
        clock.Advance(TimeSpan.FromDays(2));

        var stopping = loop.StopAsync();
        Assert.False(stopping.IsCompleted);
        blocker.Release();
        await stopping.WaitAsync(TestTimeout);

        Assert.Equal(0, callbacks);
        Assert.Equal(1, commands);
        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer("closed", TimeSpan.FromSeconds(1), () => { }));
    }

    [Fact]
    public async Task StopAsync_WaitsForAlreadyClaimedTimerCallback()
    {
        var clock = new ManualTimeProvider();
        var timers = CreateTimers(clock);
        using var loop = CreateLoop(timers, clock);
        using var blocker = new BlockingGameLoopWorkItem();
        await loop.StartAsync();
        timers.RegisterTimer("active", TimeSpan.FromMilliseconds(1), blocker.Execute, repeat: true);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await loop.PostAsync(new ActionGameLoopWorkItem(() => { }));
        await blocker.Entered.WaitAsync(TestTimeout);

        var stopping = loop.StopAsync();
        Assert.False(stopping.IsCompleted);
        Assert.False(loop.Completion.IsCompleted);
        blocker.Release();
        await stopping.WaitAsync(TestTimeout);

        Assert.Equal(1, timers.GetMetricsSnapshot().ExecutedCallbacks);
        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
    }

    [Fact]
    public async Task TimerCallback_RunsOnSameDedicatedThreadAsCommands()
    {
        var clock = new ManualTimeProvider(1000);
        var timers = CreateTimers(clock);
        using var loop = CreateLoop(timers, clock);
        var commandThread = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timerThread =
            new TaskCompletionSource<(int Id, bool OnLoop)>(TaskCreationOptions.RunContinuationsAsynchronously);
        await loop.StartAsync();
        timers.RegisterTimer(
            "tick",
            TimeSpan.FromMilliseconds(10),
            () =>
                timerThread.SetResult((Environment.CurrentManagedThreadId, loop.IsOnLoopThread))
        );
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await loop.PostAsync(new ActionGameLoopWorkItem(() => commandThread.SetResult(Environment.CurrentManagedThreadId)));

        var timer = await timerThread.Task.WaitAsync(TestTimeout);

        Assert.Equal(await commandThread.Task.WaitAsync(TestTimeout), timer.Id);
        Assert.True(timer.OnLoop);
        await loop.StopAsync();
    }

    [Fact]
    public async Task TimerFault_PropagatesOriginalFailureThroughHostAndDisposesContainer()
    {
        var clock = new ManualTimeProvider();
        var timers = CreateTimers(clock);
        using var container = new Container();
        using var cancellation = new CancellationTokenSource();
        var loop = CreateLoop(timers, clock);
        container.RegisterMoongateService<ITimerService, TimerWheelService>(timers, -900);
        container.RegisterMoongateService<IGameLoopService, GameLoopService>(loop, -800);
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var failure = new InvalidOperationException("timer failed");
        await bootstrap.StartAsync();
        var run = MoongateServerRunner.RunAsync(bootstrap);

        try
        {
            timers.RegisterTimer("fault", TimeSpan.FromMilliseconds(1), () => throw failure);
            await loop.PostAsync(new ActionGameLoopWorkItem(() => clock.Advance(TimeSpan.FromMilliseconds(1))));

            var actual = await Record.ExceptionAsync(() => run.WaitAsync(TestTimeout));

            Assert.Same(failure, actual);
            Assert.True(container.IsDisposed);
            Assert.Equal(1, timers.GetMetricsSnapshot().CallbackFaults);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TestTimeout)
                     .ConfigureAwait(
                         ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                     );
        }
    }

    private static GameLoopService CreateLoop(TimerWheelService timers, TimeProvider clock, int commandBatch = 4)
        => new(
            new() { QueueCapacity = 8, MaxWorkItemsPerBatch = commandBatch },
            timers,
            clock
        );

    private static TimerWheelService CreateTimers(TimeProvider clock, int timerBatch = 4)
        => new(
            new()
            {
                TickDuration = TimeSpan.FromMilliseconds(1), WheelSize = 8,
                MaxCallbacksPerBatch = timerBatch, CallbackBudget = TimeSpan.FromMilliseconds(5)
            },
            clock
        );
}
