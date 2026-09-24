using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Timing;

namespace Moongate.Tests.Integration.GameLoop;

public sealed class GameLoopMetricsTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task HandlerFault_DiagnosticClockAlsoFails_PreservesOriginalFailure()
    {
        var clock = new FaultingTimeProvider(new());
        var timers = new TimerWheelService(new(), clock);
        using var loop = new GameLoopService(new(), timers, clock);
        var failure = new ApplicationException("original command fault");
        await loop.StartAsync();
        await loop.PostAsync(
            new ActionGameLoopWorkItem(
                () =>
                {
                    clock.FailTimestampReads = true;

                    throw failure;
                }
            )
        );

        var actual = await Record.ExceptionAsync(() => loop.Completion.WaitAsync(TestTimeout));
        await loop.StopAsync();

        Assert.Same(failure, actual);
        Assert.Equal(1, loop.GetMetricsSnapshot().ExecutedWorkItems);
    }

    [Fact]
    public async Task HandlerFault_RecordsAttemptAndDurationWithoutNegativeQueueDepth()
    {
        var clock = new ManualTimeProvider(1000);
        var timers = new TimerWheelService(new(), clock);
        using var loop = new GameLoopService(new(), timers, clock);
        var failure = new InvalidOperationException("fatal command");
        await loop.StartAsync();
        await loop.PostAsync(
            new ActionGameLoopWorkItem(
                () =>
                {
                    clock.Advance(TimeSpan.FromMilliseconds(4));

                    throw failure;
                }
            )
        );
        Assert.Same(failure, await Record.ExceptionAsync(() => loop.Completion.WaitAsync(TestTimeout)));
        await loop.StopAsync();

        var metrics = loop.GetMetricsSnapshot();

        Assert.Equal(1, metrics.AcceptedWorkItems);
        Assert.Equal(1, metrics.ExecutedWorkItems);
        Assert.Equal(1, metrics.Faults);
        Assert.Equal(0, metrics.QueueDepth);
        Assert.Equal(TimeSpan.FromMilliseconds(4), metrics.MaxHandlerDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(4), metrics.LastBatchDuration);
    }

    [Fact]
    public async Task QueueSnapshot_ReportsDepthAgeAcceptedAndRejectedUsingTimestampFrequency()
    {
        var clock = new ManualTimeProvider(1000);
        var timers = new TimerWheelService(new(), clock);
        using var loop = new GameLoopService(new() { QueueCapacity = 1 }, timers, clock);
        using var blocker = new BlockingGameLoopWorkItem();
        await loop.StartAsync();
        await loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(TestTimeout);
        await loop.PostAsync(new ActionGameLoopWorkItem(() => clock.Advance(TimeSpan.FromMilliseconds(3))));
        Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        clock.Advance(TimeSpan.FromMilliseconds(7));

        var queued = loop.GetMetricsSnapshot();

        Assert.Equal(1, queued.QueueDepth);
        Assert.Equal(TimeSpan.FromMilliseconds(7), queued.OldestQueuedItemAge);
        Assert.Equal(2, queued.AcceptedWorkItems);
        Assert.Equal(1, queued.RejectedWorkItems);
        Assert.Equal(1, queued.ExecutedWorkItems);
        blocker.Release();
        await loop.StopAsync().WaitAsync(TestTimeout);
        var stopped = loop.GetMetricsSnapshot();
        Assert.Equal(0, stopped.QueueDepth);
        Assert.Equal(TimeSpan.Zero, stopped.OldestQueuedItemAge);
        Assert.Equal(2, stopped.ExecutedWorkItems);
        Assert.Equal(0, stopped.Faults);
        Assert.True(stopped.MaxHandlerDuration >= TimeSpan.FromMilliseconds(7));
    }
}
