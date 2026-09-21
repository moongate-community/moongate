using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.Timing;

namespace Moongate.Tests.Server.Services.Timing;

public sealed class TimerWheelServiceTests
{
    [Fact]
    public void OneShot_RoundsUpAcrossProviderFrequencyAndFirstProcessing()
    {
        var clock = new ManualTimeProvider(1000);
        var timers = Create(clock);
        var count = 0;
        timers.RegisterTimer("once", TimeSpan.FromMilliseconds(9), () => count++);
        clock.Advance(TimeSpan.FromMilliseconds(15));
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(1, count);
        Assert.Null(timers.GetNextDelay());
    }

    [Fact]
    public void InitialDelay_OverridesIntervalAcrossMultipleWheelRounds()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        var count = 0;
        timers.RegisterTimer("delayed", TimeSpan.FromMilliseconds(8), () => count++, TimeSpan.FromMilliseconds(136));
        clock.Advance(TimeSpan.FromMilliseconds(128));
        Assert.Equal(0, timers.ProcessDueTimers());
        Assert.Equal(TimeSpan.FromMilliseconds(8), timers.GetNextDelay());
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(1, count);
    }

    [Fact]
    public void Registration_UsesCurrentTimeBetweenBoundariesAndAfterStaleWheel()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        clock.Advance(TimeSpan.FromMilliseconds(1003));
        timers.RegisterTimer("fresh", TimeSpan.FromMilliseconds(8), () => { });
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(5));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void EqualDeadlineCallbacks_PreserveRegistrationOrder()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        var order = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            var value = i;
            timers.RegisterTimer("same", TimeSpan.FromMilliseconds(8), () => order.Add(value));
        }

        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(4, timers.ProcessDueTimers());
        Assert.Equal(new[] { 0, 1, 2, 3 }, order);
    }

    [Fact]
    public void Cancellation_ByIdAndNameKeepsRegistrationsDistinct()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        var first = timers.RegisterTimer("same", TimeSpan.FromMilliseconds(8), () => { });
        var second = timers.RegisterTimer("same", TimeSpan.FromMilliseconds(8), () => { });
        Assert.NotEqual(first, second);
        Assert.True(timers.UnregisterTimer(first));
        Assert.False(timers.UnregisterTimer(first));
        Assert.Equal(1, timers.UnregisterTimersByName("same"));
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(0, timers.ProcessDueTimers());
        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
    }

    [Fact]
    public void Callback_CanCancelAnotherReadyCallbackBeforeClaim()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        string other = "";
        timers.RegisterTimer("first", TimeSpan.FromMilliseconds(8), () => Assert.True(timers.UnregisterTimer(other)));
        other = timers.RegisterTimer("second", TimeSpan.FromMilliseconds(8), () => Assert.Fail("Cancelled callback ran"));
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void RepeatingCallback_CanCancelItselfOrAllTimers(bool all)
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        string id = "";
        id = timers.RegisterTimer(
            "repeat",
            TimeSpan.FromMilliseconds(8),
            () =>
            {
                if (all)
                {
                    timers.UnregisterAllTimers();
                }
                else
                {
                    Assert.True(timers.UnregisterTimer(id));
                }
            },
            repeat: true
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(80));
        Assert.Equal(0, timers.ProcessDueTimers());
    }

    [Fact]
    public void Repeat_CoalescesLongPauseAndMaintainsFixedRate()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        timers.RegisterTimer("repeat", TimeSpan.FromMilliseconds(16), () => { }, repeat: true);
        clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Equal(1, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(61, timers.GetMetricsSnapshot().CoalescedOccurrences);
        Assert.Equal(TimeSpan.FromMilliseconds(8), timers.GetNextDelay());
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void CallbackRegistration_AfterClockJumpStartsAtCurrentTime()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        timers.RegisterTimer(
            "parent",
            TimeSpan.FromMilliseconds(8),
            () =>
                timers.RegisterTimer("child", TimeSpan.FromMilliseconds(8), () => { })
        );
        clock.Advance(TimeSpan.FromDays(365));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void CountLimit_RetainsReadyBacklogAndCancellationFreesCapacity()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock, new TimerWheelOptions { MaxCallbacksPerBatch = 1, MaxPendingTimers = 2 });
        timers.RegisterTimer("first", TimeSpan.FromMilliseconds(8), () => { });
        var second = timers.RegisterTimer("second", TimeSpan.FromMilliseconds(8), () => { });
        Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                "full",
                TimeSpan.FromMilliseconds(8),
                () => { }
            )
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        timers.RegisterTimer("future", TimeSpan.FromMilliseconds(8), () => { });
        Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                "still-full",
                TimeSpan.FromMilliseconds(8),
                () => { }
            )
        );
        Assert.True(timers.UnregisterTimer(second));
        timers.RegisterTimer("replacement", TimeSpan.FromMilliseconds(8), () => { });
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(TimeSpan.Zero, timers.GetNextDelay());
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void CallbackBudget_DoesNotClaimNextReadyEntry()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        timers.RegisterTimer("slow", TimeSpan.FromMilliseconds(8), () => clock.Advance(TimeSpan.FromMilliseconds(5)));
        var second = timers.RegisterTimer("next", TimeSpan.FromMilliseconds(8), () => Assert.Fail("Cancelled callback ran"));
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(TimeSpan.FromMilliseconds(5), timers.GetMetricsSnapshot().LastBatchDuration);
        Assert.True(timers.UnregisterTimer(second));
        Assert.Equal(0, timers.ProcessDueTimers());
    }

    [Fact]
    public void RepeatingInFlightRegistration_StillConsumesCapacity()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock, new TimerWheelOptions { MaxPendingTimers = 1 });
        timers.RegisterTimer(
            "repeat",
            TimeSpan.FromMilliseconds(8),
            () =>
                Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                        "full",
                        TimeSpan.FromMilliseconds(8),
                        () => { }
                    )
                ),
            repeat: true
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(1, timers.GetMetricsSnapshot().ActiveTimers);
    }

    [Fact]
    public void Driver_RejectsUnboundForeignAndReentrantExecution()
    {
        var clock = new ManualTimeProvider();
        var unbound = new TimerWheelService(new TimerWheelOptions(), clock);
        Assert.Throws<InvalidOperationException>(() => unbound.ProcessDueTimers());
        var timers = Create(clock);
        Exception? foreign = null;
        var thread = new Thread(() => foreign = Record.Exception(() => timers.ProcessDueTimers()));
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(foreign);
        Assert.Throws<InvalidOperationException>(() => timers.BindToCurrentThread(() => { }));
        var owner = Environment.CurrentManagedThreadId;
        timers.RegisterTimer(
            "owner",
            TimeSpan.FromMilliseconds(8),
            () =>
            {
                Assert.Equal(owner, Environment.CurrentManagedThreadId);
                Assert.Throws<InvalidOperationException>(() => timers.ProcessDueTimers());
            }
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void CallbackFault_PropagatesOriginalAndPermanentlyClosesAdmission()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        var failure = new ApplicationException("timer failure");
        timers.RegisterTimer("fault", TimeSpan.FromMilliseconds(8), () => throw failure, repeat: true);
        timers.RegisterTimer("abandoned", TimeSpan.FromMilliseconds(8), () => Assert.Fail("Abandoned callback ran"));
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => timers.ProcessDueTimers()));
        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Equal(1, timers.GetMetricsSnapshot().ExecutedCallbacks);
        Assert.Equal(1, timers.GetMetricsSnapshot().CallbackFaults);
        Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                "closed",
                TimeSpan.FromMilliseconds(8),
                () => { }
            )
        );
    }

    [Fact]
    public async Task Stop_BeforeStartPermanentlyClosesAndCancels()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        timers.RegisterTimer("pending", TimeSpan.FromMilliseconds(8), () => Assert.Fail("Stopped timer ran"));
        await timers.StopAsync();
        await timers.StopAsync();
        Assert.Null(timers.GetNextDelay());
        await Assert.ThrowsAsync<InvalidOperationException>(() => timers.StartAsync());
        Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                "closed",
                TimeSpan.FromMilliseconds(8),
                () => { }
            )
        );
    }

    [Fact]
    public void WakeCallback_ObservesEarlierDeadlineWithoutHoldingRegistryLock()
    {
        var clock = new ManualTimeProvider();
        var timers = new TimerWheelService(new TimerWheelOptions(), clock);
        var observed = new List<TimeSpan?>();
        timers.BindToCurrentThread(() =>
            {
                TimeSpan? delay = null;
                var thread = new Thread(() => delay = timers.GetNextDelay());
                thread.Start();
                Assert.True(thread.Join(TimeSpan.FromSeconds(2)), "Wake callback held registry lock");
                observed.Add(delay);
            }
        );
        timers.RegisterTimer("later", TimeSpan.FromMilliseconds(80), () => { });
        timers.RegisterTimer("earlier", TimeSpan.FromMilliseconds(8), () => { });
        Assert.Equal(new TimeSpan?[] { TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(8) }, observed);
    }

    [Fact]
    public void Metrics_MeasureLatenessDurationAndAttemptsWithProviderFrequency()
    {
        var clock = new ManualTimeProvider(1000);
        var timers = Create(clock);
        timers.RegisterTimer("slow", TimeSpan.FromMilliseconds(8), () => clock.Advance(TimeSpan.FromMilliseconds(3)));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        timers.ProcessDueTimers();
        var metrics = timers.GetMetricsSnapshot();
        Assert.Equal(1, metrics.RegisteredTimers);
        Assert.Equal(1, metrics.ExecutedCallbacks);
        Assert.Equal(0, metrics.ActiveTimers);
        Assert.Equal(TimeSpan.FromMilliseconds(2), metrics.MaxLateness);
        Assert.Equal(TimeSpan.FromMilliseconds(3), metrics.MaxCallbackDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(3), metrics.LastBatchDuration);
    }

    [Fact]
    public void Registration_RejectsInvalidArgumentsAndOverflowWithoutRetainingEntries()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        Assert.Throws<ArgumentException>(() => timers.RegisterTimer(" ", TimeSpan.FromSeconds(1), () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => timers.RegisterTimer("zero", TimeSpan.Zero, () => { }));
        Assert.Throws<ArgumentNullException>(() => timers.RegisterTimer("null", TimeSpan.FromSeconds(1), null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => timers.RegisterTimer(
                "delay",
                TimeSpan.FromSeconds(1),
                () => { },
                TimeSpan.Zero
            )
        );
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Throws<OverflowException>(() => timers.RegisterTimer("overflow", TimeSpan.MaxValue, () => { }));
        Assert.Equal(0, timers.GetMetricsSnapshot().RegisteredTimers);
        Assert.Null(timers.GetNextDelay());
    }

    [Fact]
    public void Options_RejectNonpositiveLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheelOptions { TickDuration = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheelOptions { WheelSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheelOptions { MaxPendingTimers = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheelOptions { MaxCallbacksPerBatch = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheelOptions { CallbackBudget = TimeSpan.Zero });
    }

    [Fact]
    public void Metrics_IncludeTheCurrentlyAttemptedCallback()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        timers.RegisterTimer(
            "in-flight",
            TimeSpan.FromMilliseconds(8),
            () =>
                Assert.Equal(1, timers.GetMetricsSnapshot().ExecutedCallbacks)
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void Registration_AtFractionalTimeSpanTickNeverRoundsEarly()
    {
        var clock = new ManualTimeProvider(3);
        var timers = Create(clock, new TimerWheelOptions { TickDuration = TimeSpan.FromTicks(1), WheelSize = 2 });
        clock.Advance(TimeSpan.FromMilliseconds(334)); // One provider tick: 1/3 second.
        timers.RegisterTimer("fraction", TimeSpan.FromTicks(3333333), () => { });
        clock.Advance(TimeSpan.FromMilliseconds(334)); // 2/3 second, before the rounded-up deadline.
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMilliseconds(334));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Fact]
    public void LongPause_PreservesAbsoluteDeadlineOrderAcrossWrappedBuckets()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        var order = new List<int>();
        timers.RegisterTimer("last", TimeSpan.FromMilliseconds(128), () => order.Add(3));
        timers.RegisterTimer("first", TimeSpan.FromMilliseconds(8), () => order.Add(1));
        timers.RegisterTimer("middle", TimeSpan.FromMilliseconds(72), () => order.Add(2));
        clock.Advance(TimeSpan.FromDays(3650));
        Assert.Equal(3, timers.ProcessDueTimers());
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void Repeat_CallbackDurationCoalescesMissedPeriodsFromCompletionTime()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        timers.RegisterTimer(
            "slow-repeat",
            TimeSpan.FromMilliseconds(8),
            () => clock.Advance(TimeSpan.FromMilliseconds(24)),
            repeat: true
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(3, timers.GetMetricsSnapshot().CoalescedOccurrences);
        Assert.Equal(TimeSpan.FromMilliseconds(8), timers.GetNextDelay());
        Assert.Equal(0, timers.ProcessDueTimers());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Close_DuringClaimedCallbackAllowsFinishButPreventsRepeat(bool cancelFirst)
    {
        var clock = new ManualTimeProvider();
        var timers = new TimerWheelService(new TimerWheelOptions(), clock);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var finished = false;
        var id = timers.RegisterTimer(
            "in-flight",
            TimeSpan.FromMilliseconds(8),
            () =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
                finished = true;
            },
            repeat: true
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Exception? failure = null;
        var thread = new Thread(() =>
            {
                failure = Record.Exception(() =>
                    {
                        timers.BindToCurrentThread(() => { });
                        Assert.Equal(1, timers.ProcessDueTimers());
                        Assert.Equal(0, timers.ProcessDueTimers());
                    }
                );
            }
        );
        thread.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            if (cancelFirst)
            {
                Assert.True(timers.UnregisterTimer(id));
            }

            timers.Close();
            Assert.False(finished);
            Assert.Null(timers.GetNextDelay());
            Assert.Throws<InvalidOperationException>(() => timers.RegisterTimer(
                    "closed",
                    TimeSpan.FromMilliseconds(8),
                    () => { }
                )
            );
        }
        finally
        {
            release.Set();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.Null(failure);
        Assert.True(finished);
        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
    }

    [Fact]
    public void ConcurrentRegistrationsAndClose_LeaveNoAcceptedTimerRetained()
    {
        var clock = new ManualTimeProvider();
        var timers = Create(clock);
        using var start = new ManualResetEventSlim();
        var writers = Enumerable.Range(0, 4)
            .Select(_ => new Thread(() =>
                    {
                        start.Wait();
                        for (var i = 0; i < 100; i++)
                        {
                            try
                            {
                                timers.RegisterTimer(
                                    "race",
                                    TimeSpan.FromMilliseconds(8),
                                    () => Assert.Fail("Closed callback ran")
                                );
                            }
                            catch (InvalidOperationException)
                            {
                                break;
                            }
                        }
                    }
                )
            )
            .ToArray();
        foreach (var writer in writers)
        {
            writer.Start();
        }

        start.Set();
        timers.Close();
        foreach (var writer in writers)
        {
            Assert.True(writer.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Null(timers.GetNextDelay());
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(0, timers.ProcessDueTimers());
    }

    [Fact]
    public void CallbackFault_RemainsOriginalWhenDiagnosticClockAlsoFails()
    {
        var clock = new ManualTimeProvider();
        var faultingClock = new FaultingTimeProvider(clock);
        var timers = new TimerWheelService(new TimerWheelOptions(), faultingClock);
        timers.BindToCurrentThread(() => { });
        var failure = new ApplicationException("original callback failure");
        timers.RegisterTimer(
            "fault",
            TimeSpan.FromMilliseconds(8),
            () =>
            {
                faultingClock.FailTimestampReads = true;
                throw failure;
            }
        );
        clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => timers.ProcessDueTimers()));
        Assert.Equal(1, timers.GetMetricsSnapshot().CallbackFaults);
        Assert.Equal(1, timers.GetMetricsSnapshot().ExecutedCallbacks);
        Assert.Equal(0, timers.GetMetricsSnapshot().ActiveTimers);
        faultingClock.FailTimestampReads = false;
        Assert.Equal(0, timers.ProcessDueTimers());
    }

    [Fact]
    public void PartialProviderTicks_AccumulateBeforeDeadlineProcessing()
    {
        var clock = new ManualTimeProvider(1000);
        var timers = Create(clock, new TimerWheelOptions { TickDuration = TimeSpan.FromMilliseconds(1) });
        timers.RegisterTimer("partial", TimeSpan.FromMilliseconds(1), () => { });
        clock.Advance(TimeSpan.FromMicroseconds(500));
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.Advance(TimeSpan.FromMicroseconds(500));
        Assert.Equal(1, timers.ProcessDueTimers());
    }

    [Theory, InlineData(4, 0, 6), InlineData(5, 0, 6), InlineData(6, 1, 9), InlineData(7, 1, 9)]
    public void Repeat_FractionalCompletionOnlyCoalescesElapsedOccurrences(
        long firstTimestamp, long expectedCoalesced, long nextTimestamp
    )
    {
        var clock = new RawTimestampTimeProvider(30_000_000);
        var timers = new TimerWheelService(new TimerWheelOptions { TickDuration = TimeSpan.FromTicks(1) }, clock);
        timers.BindToCurrentThread(() => { });
        timers.RegisterTimer("fractional-repeat", TimeSpan.FromTicks(1), () => { }, repeat: true);

        // Three provider ticks equal one TimeSpan tick; the next integer deadline may still be in the future.
        clock.AdvanceTimestamp(firstTimestamp);
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(expectedCoalesced, timers.GetMetricsSnapshot().CoalescedOccurrences);
        clock.AdvanceTimestamp(nextTimestamp - firstTimestamp - 1);
        Assert.Equal(0, timers.ProcessDueTimers());
        clock.AdvanceTimestamp(1);
        Assert.Equal(1, timers.ProcessDueTimers());
        Assert.Equal(expectedCoalesced, timers.GetMetricsSnapshot().CoalescedOccurrences);
    }

    private static TimerWheelService Create(ManualTimeProvider clock, TimerWheelOptions? options = null)
    {
        var timers = new TimerWheelService(options ?? new TimerWheelOptions { WheelSize = 8 }, clock);
        timers.BindToCurrentThread(() => { });
        return timers;
    }
}
