using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.AI;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Serilog.Events;

namespace Moongate.Tests.Server.AI;

[Collection(GlobalSerilogCollection.Name)]
public class NpcBrainSchedulerTests
{
    [Fact]
    public void Bind_SleepingSector_BindsAndRetainsRuntimeWithoutInvoking()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddMobile(0x1);

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        Assert.Equal([(mobile.Id, "guard")], fixture.Runtime.BindCalls);
        Assert.Empty(fixture.Runtime.Invocations);
        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
        Assert.True(fixture.Scheduler.TryGetDescriptor(mobile.Id, out var descriptor));
        Assert.Equal("guard", descriptor!.BrainId);
        Assert.Equal(1, fixture.Metrics.Current.SleepingBrains);
        Assert.Equal(1, fixture.Metrics.Current.SleepingSectors);
    }

    [Fact]
    public void Activate_GraceSector_InvokesActivateBeforeImmediateThink()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddMobile(0x1);
        fixture.Sectors.SetActive(mobile, true);

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        Assert.Equal(
            [NpcBrainHookType.Activate, NpcBrainHookType.Think],
            fixture.Runtime.Invocations.Select(invocation => invocation.Hook)
        );
        Assert.True(fixture.Scheduler.IsActive(mobile.Id));
        Assert.Equal(1, fixture.Metrics.Current.ActiveBrains);
        Assert.Equal(2, fixture.Metrics.Current.HookInvocations);
        Assert.Equal(1, fixture.Metrics.Current.BrainsExecuted);
    }

    [Fact]
    public void Tick_HookInvocation_UsesInjectedTimeProviderForDurationMetrics()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Runtime.InvocationHandler = (_, _, _, _) =>
        {
            fixture.Time.Advance(TimeSpan.FromMilliseconds(25));
            return NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        };

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        Assert.Equal(2, fixture.Metrics.Current.HookInvocations);
        Assert.Equal(TimeSpan.FromMilliseconds(50).Ticks, fixture.Metrics.Current.TotalHookDurationTicks);
        Assert.Equal(TimeSpan.FromMilliseconds(25).Ticks, fixture.Metrics.Current.MaxHookDurationTicks);
        Assert.Equal(TimeSpan.FromMilliseconds(25), fixture.Metrics.Current.AverageHookDuration);
    }

    [Fact]
    public void Deactivate_ActiveBrain_InvokesOnceClearsMailboxAndSleeps()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Runtime.Invocations.Clear();
        fixture.Scheduler.EnqueueEvent(
            mobile.Id,
            NpcBrainHookType.SpeechHeard,
            Event(NpcBrainEventType.SpeechHeard, 0x2)
        );

        fixture.Scheduler.Deactivate(mobile.Id);

        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
        fixture.Scheduler.Tick();
        Assert.Equal([NpcBrainHookType.Deactivate], fixture.Runtime.Invocations.Select(invocation => invocation.Hook));
        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
        Assert.Equal(1, fixture.Metrics.Current.SleepingBrains);

        fixture.Runtime.Invocations.Clear();
        fixture.Scheduler.Activate(mobile.Id);
        fixture.Scheduler.Tick();
        Assert.Equal(
            [NpcBrainHookType.Activate, NpcBrainHookType.Think],
            fixture.Runtime.Invocations.Select(invocation => invocation.Hook)
        );
    }

    [Fact]
    public void Deactivate_FailedHook_RecordsInvocationAndStillSleepsWithoutRetry()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Deactivate
                ? NpcBrainInvocationResult.Failed("deactivate failed")
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Runtime.Invocations.Clear();

        fixture.Scheduler.Deactivate(mobile.Id);
        fixture.Scheduler.Tick();

        Assert.Equal([NpcBrainHookType.Deactivate], fixture.Runtime.Invocations.Select(invocation => invocation.Hook));
        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
        Assert.Equal(3, fixture.Metrics.Current.HookInvocations);

        fixture.Time.Advance(TimeSpan.FromMinutes(2));
        fixture.Scheduler.Tick();
        Assert.Single(fixture.Runtime.Invocations);
    }

    [Fact]
    public void SleepWake_Cycle_PreservesBindingAndRuntimeState()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Scheduler.Deactivate(mobile.Id);
        fixture.Scheduler.Tick();

        fixture.Scheduler.Activate(mobile.Id);
        fixture.Scheduler.Tick();

        Assert.Single(fixture.Runtime.BindCalls);
        Assert.Empty(fixture.Runtime.ResetCalls);
        Assert.Empty(fixture.Runtime.UnbindCalls);
        Assert.Equal(2, fixture.Runtime.Invocations.Count(invocation => invocation.Hook == NpcBrainHookType.Activate));
        Assert.Single(fixture.Runtime.Invocations, invocation => invocation.Hook == NpcBrainHookType.Deactivate);
    }

    [Fact]
    public void Bind_SameBrainAfterMove_PreservesOriginalHome()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddMobile(0x1, x: 10, y: 10);
        fixture.Scheduler.Bind(mobile);
        fixture.Move(mobile, 30, 40);

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Activate(mobile.Id);
        fixture.Scheduler.Tick();

        var context = Assert.Single(
            fixture.Runtime.Invocations,
            invocation => invocation.Hook == NpcBrainHookType.Activate
        ).Context;
        Assert.Equal(new Point3D(10, 10, 0), context.HomePosition);
        Assert.Single(fixture.Runtime.BindCalls);
    }

    [Fact]
    public void Bind_ChangedBrain_ResetsRebindsAndRecapturesHome()
    {
        var fixture = new SchedulerFixture();
        fixture.Runtime.Descriptors["wanderer"] = new("wanderer", 500, 7, 9);
        var mobile = fixture.AddMobile(0x1, x: 10, y: 10);
        fixture.Scheduler.Bind(mobile);
        fixture.Move(mobile, 30, 40);
        mobile.BrainScriptId = "wanderer";
        fixture.Sectors.SetActive(mobile, true);

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        Assert.Equal([mobile.Id], fixture.Runtime.ResetCalls);
        Assert.Equal([(mobile.Id, "guard"), (mobile.Id, "wanderer")], fixture.Runtime.BindCalls);
        var activation = Assert.Single(
            fixture.Runtime.Invocations,
            invocation => invocation.Hook == NpcBrainHookType.Activate
        );
        Assert.Equal(new Point3D(30, 40, 0), activation.Context.HomePosition);
        Assert.Equal(0, activation.Context.HomeMapId);
    }

    [Fact]
    public void Bind_ChangedBrainUnavailable_DoesNotInvokeStaleRuntimeBindingAndRetriesNewBrain()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Runtime.Invocations.Clear();
        mobile.BrainScriptId = "missing";

        fixture.Scheduler.Bind(mobile);
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Scheduler.Tick();

        Assert.Empty(fixture.Runtime.Invocations);
        Assert.Equal(
            [(mobile.Id, "guard"), (mobile.Id, "missing"), (mobile.Id, "missing")],
            fixture.Runtime.BindCalls
        );
        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
    }

    [Fact]
    public void Bind_RepeatedAfterUnavailableBrainChange_DoesNotRestoreOldDescriptorRangesOrRuntime()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Runtime.Invocations.Clear();
        mobile.BrainScriptId = "missing";

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Bind(mobile);

        Assert.False(fixture.Scheduler.TryGetDescriptor(mobile.Id, out _));
        Assert.Equal(0, fixture.Scheduler.MaxPerceptionRange);
        Assert.Equal(0, fixture.Scheduler.MaxHearingRange);

        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Scheduler.Tick();

        Assert.Empty(fixture.Runtime.Invocations);
    }

    [Fact]
    public void Unbind_BoundBrain_RemovesSchedulerAndRuntimeState()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);

        fixture.Scheduler.Unbind(mobile.Id);
        fixture.Scheduler.Tick();

        Assert.Equal([mobile.Id], fixture.Runtime.UnbindCalls);
        Assert.False(fixture.Scheduler.TryGetDescriptor(mobile.Id, out _));
        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
        Assert.Empty(fixture.Runtime.Invocations);
        Assert.Equal(0, fixture.Metrics.Current.ActiveBrains);
    }

    [Fact]
    public void Tick_QueuedEventAndThinkDue_InvokesEventsBeforeThink()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.EnqueueEvent(
            mobile.Id,
            NpcBrainHookType.SpeechHeard,
            Event(NpcBrainEventType.SpeechHeard, 0x2)
        );

        fixture.Scheduler.Tick();

        Assert.Equal(
            [NpcBrainHookType.Activate, NpcBrainHookType.SpeechHeard, NpcBrainHookType.Think],
            fixture.Runtime.Invocations.Select(invocation => invocation.Hook)
        );
    }

    [Fact]
    public void Tick_MoreEventsThanWakeBudget_DefersRemainderToNextWake()
    {
        var fixture = new SchedulerFixture(maxEventsPerWake: 2);
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Runtime.Invocations.Clear();

        fixture.EnqueueSpeech(mobile.Id, 0x2);
        fixture.EnqueueSpeech(mobile.Id, 0x3);
        fixture.EnqueueSpeech(mobile.Id, 0x4);
        fixture.Scheduler.Tick();

        Assert.Equal(2, fixture.Runtime.Invocations.Count);
        fixture.Scheduler.Tick();
        Assert.Equal(3, fixture.Runtime.Invocations.Count);
        Assert.Equal(4, fixture.Metrics.Current.EventsDelivered);
    }

    [Fact]
    public void Tick_MoreDueBrainsThanLoopBudget_PreservesStableFifoAndCountsEachDeferredBrain()
    {
        var fixture = new SchedulerFixture(maxBrainsPerLoop: 1);
        var first = fixture.AddActiveMobile(0x1);
        var second = fixture.AddActiveMobile(0x2);
        var third = fixture.AddActiveMobile(0x3);
        fixture.Scheduler.Bind(first);
        fixture.Scheduler.Bind(second);
        fixture.Scheduler.Bind(third);

        fixture.Scheduler.Tick();
        fixture.Scheduler.Tick();
        fixture.Scheduler.Tick();

        Assert.Equal(
            [first.Id, second.Id, third.Id],
            fixture.Runtime.Invocations
                .Where(invocation => invocation.Hook == NpcBrainHookType.Activate)
                .Select(invocation => invocation.MobileId)
        );
        Assert.Equal(3, fixture.Metrics.Current.BrainsExecuted);
        Assert.Equal(3, fixture.Metrics.Current.BrainsDeferred);
    }

    [Fact]
    public void Tick_InvalidIntent_DoesNotFaultEntry()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        var invalidIntent = new BrainIntent(BrainIntentType.Unknown, "explode", Serial.Zero, null);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think
                ? NpcBrainInvocationResult.Succeeded(new(null, [invalidIntent]))
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);

        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        var execution = Assert.Single(fixture.Intents.Executions);
        Assert.Equal(invalidIntent, Assert.Single(execution.Intents));
        Assert.True(fixture.Scheduler.IsActive(mobile.Id));
        Assert.Empty(fixture.Runtime.ResetCalls);
    }

    [Fact]
    public void Tick_ConsecutiveInvocationFailures_BackOffOneFiveThirtyThenSixtySecondsAndResetFourth()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think
                ? NpcBrainInvocationResult.Failed("think failed")
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);

        fixture.Scheduler.Tick();
        Assert.False(fixture.Scheduler.IsActive(mobile.Id));
        Assert.Equal(1, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromMilliseconds(999));
        fixture.Scheduler.Tick();
        Assert.Equal(1, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(2, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromSeconds(5));
        fixture.Scheduler.Tick();
        Assert.Equal(3, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromSeconds(30));
        fixture.Scheduler.Tick();
        Assert.Equal(4, fixture.ThinkCount(mobile.Id));
        Assert.Equal([mobile.Id], fixture.Runtime.ResetCalls);

        fixture.Time.Advance(TimeSpan.FromSeconds(60));
        fixture.Scheduler.Tick();
        Assert.Equal(5, fixture.ThinkCount(mobile.Id));
        Assert.Equal([mobile.Id], fixture.Runtime.ResetCalls);
        Assert.Empty(fixture.Intents.Executions);
    }

    [Fact]
    public void Tick_IdenticalFaults_LogsOneSchedulerWarningPerMinute()
    {
        using var logs = new GlobalSerilogCapture();
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think
                ? NpcBrainInvocationResult.Failed("think failed")
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);

        fixture.Scheduler.Tick();
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Scheduler.Tick();
        fixture.Time.Advance(TimeSpan.FromSeconds(5));
        fixture.Scheduler.Tick();
        fixture.Time.Advance(TimeSpan.FromSeconds(30));
        fixture.Scheduler.Tick();

        Assert.Single(SchedulerWarnings(logs));

        fixture.Time.Advance(TimeSpan.FromSeconds(60));
        fixture.Scheduler.Tick();

        var warnings = SchedulerWarnings(logs);
        Assert.Equal(2, warnings.Count);
        var warning = warnings[0];
        Assert.Equal("guard", Assert.IsType<ScalarValue>(warning.Properties["BrainId"]).Value);
        Assert.Equal(mobile.Id.Value, Assert.IsType<ScalarValue>(warning.Properties["MobileId"]).Value);
        Assert.Equal(NpcBrainHookType.Think, Assert.IsType<ScalarValue>(warning.Properties["Hook"]).Value);
        Assert.Equal("think failed", Assert.IsType<ScalarValue>(warning.Properties["Error"]).Value);
    }

    [Fact]
    public void Unbind_AfterFault_ClearsMobileThrottleEntry()
    {
        using var logs = new GlobalSerilogCapture();
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think
                ? NpcBrainInvocationResult.Failed("think failed")
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        fixture.Scheduler.Unbind(mobile.Id);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        Assert.Equal(2, SchedulerWarnings(logs).Count);
    }

    [Fact]
    public void Bind_ChangedBrain_ClearsMobileThrottleEntriesBeforeBrainCanReturn()
    {
        using var logs = new GlobalSerilogCapture();
        var fixture = new SchedulerFixture();
        fixture.Runtime.Descriptors["scout"] = new("scout", 1000, 20, 25);
        var mobile = fixture.AddActiveMobile(0x1);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think
                ? NpcBrainInvocationResult.Failed("think failed")
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        mobile.BrainScriptId = "scout";
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        mobile.BrainScriptId = "guard";
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        Assert.Equal(3, SchedulerWarnings(logs).Count);
    }

    [Fact]
    public void Tick_ExpiredFaultThrottleEntry_PrunesEntryWithoutAnotherFailure()
    {
        using var logs = new GlobalSerilogCapture();
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        var failThink = true;
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think && failThink
                ? NpcBrainInvocationResult.Failed("think failed")
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        Assert.Equal(1, FaultLogCount(fixture.Scheduler));

        failThink = false;
        fixture.Time.Advance(TimeSpan.FromSeconds(61));
        fixture.Scheduler.Tick();

        Assert.Equal(0, FaultLogCount(fixture.Scheduler));
        Assert.Single(SchedulerWarnings(logs));
    }

    [Fact]
    public void Tick_SuccessAfterFailures_ResetsNextFailureBackoffToOneSecond()
    {
        var fixture = new SchedulerFixture();
        var mobile = fixture.AddActiveMobile(0x1);
        var attempts = 0;
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
        {
            if (hook != NpcBrainHookType.Think)
            {
                return NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
            }

            attempts++;
            return attempts == 3
                ? NpcBrainInvocationResult.Succeeded(BrainDecision.Empty)
                : NpcBrainInvocationResult.Failed("think failed");
        };
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Scheduler.Tick();
        fixture.Time.Advance(TimeSpan.FromSeconds(5));
        fixture.Scheduler.Tick();
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(4, attempts);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(999));
        fixture.Scheduler.Tick();
        Assert.Equal(4, attempts);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(5, attempts);
    }

    [Fact]
    public void Bind_MissingDescriptor_RetriesBindingWithFaultBackoff()
    {
        var fixture = new SchedulerFixture();
        fixture.Runtime.Descriptors.Remove("guard");
        var mobile = fixture.AddActiveMobile(0x1);

        fixture.Scheduler.Bind(mobile);
        Assert.Single(fixture.Runtime.BindCalls);
        fixture.Scheduler.Tick();
        Assert.Single(fixture.Runtime.BindCalls);

        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(2, fixture.Runtime.BindCalls.Count);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(4999));
        fixture.Scheduler.Tick();
        Assert.Equal(2, fixture.Runtime.BindCalls.Count);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(3, fixture.Runtime.BindCalls.Count);

        fixture.Runtime.Descriptors["guard"] = new("guard", 1000, 10, 15);
        fixture.Time.Advance(TimeSpan.FromSeconds(30));
        fixture.Scheduler.Tick();

        Assert.Equal(4, fixture.Runtime.BindCalls.Count);
        Assert.Equal(
            [NpcBrainHookType.Activate, NpcBrainHookType.Think],
            fixture.Runtime.Invocations.Select(invocation => invocation.Hook)
        );
        Assert.True(fixture.Scheduler.IsActive(mobile.Id));
    }

    [Fact]
    public void RefreshDescriptor_ChangedRanges_RecomputesGlobalMaximums()
    {
        var fixture = new SchedulerFixture();
        fixture.Runtime.Descriptors["scout"] = new("scout", 1000, 20, 25);
        fixture.Scheduler.Bind(fixture.AddMobile(0x1));
        fixture.Scheduler.Bind(fixture.AddMobile(0x2, brainId: "scout"));
        Assert.Equal(20, fixture.Scheduler.MaxPerceptionRange);
        Assert.Equal(25, fixture.Scheduler.MaxHearingRange);

        fixture.Scheduler.RefreshDescriptor("scout", new("scout", 1000, 4, 5));

        Assert.Equal(10, fixture.Scheduler.MaxPerceptionRange);
        Assert.Equal(15, fixture.Scheduler.MaxHearingRange);
        Assert.True(fixture.Scheduler.TryGetDescriptor(new Serial(0x2), out var descriptor));
        Assert.Equal(4, descriptor!.PerceptionRange);
    }

    [Fact]
    public void Tick_ThinkOverride_ClampsToConfiguredMinimumAndMaximum()
    {
        var fixture = new SchedulerFixture(minTickMilliseconds: 100, maxTickMilliseconds: 1000);
        var mobile = fixture.AddActiveMobile(0x1);
        var thinkResults = new Queue<int?>([1, 5000, null]);
        fixture.Runtime.InvocationHandler = (_, hook, _, _) =>
            hook == NpcBrainHookType.Think
                ? NpcBrainInvocationResult.Succeeded(new(thinkResults.Dequeue(), []))
                : NpcBrainInvocationResult.Succeeded(BrainDecision.Empty);
        fixture.Scheduler.Bind(mobile);
        fixture.Scheduler.Tick();

        fixture.Time.Advance(TimeSpan.FromMilliseconds(99));
        fixture.Scheduler.Tick();
        Assert.Equal(1, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(2, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromMilliseconds(999));
        fixture.Scheduler.Tick();
        Assert.Equal(2, fixture.ThinkCount(mobile.Id));

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        fixture.Scheduler.Tick();
        Assert.Equal(3, fixture.ThinkCount(mobile.Id));
    }

    [Fact]
    public void Tick_SleepingBrains_GaugesDistinctCurrentOccupiedSectorsWithoutRetainingEmptyKeys()
    {
        var fixture = new SchedulerFixture();
        var first = fixture.AddMobile(0x1, x: 1, y: 1);
        var sameSector = fixture.AddMobile(0x2, x: 2, y: 2);
        var otherSector = fixture.AddMobile(0x3, x: 32, y: 32);
        fixture.Scheduler.Bind(first);
        fixture.Scheduler.Bind(sameSector);
        fixture.Scheduler.Bind(otherSector);
        fixture.Scheduler.Tick();
        Assert.Equal(3, fixture.Metrics.Current.SleepingBrains);
        Assert.Equal(2, fixture.Metrics.Current.SleepingSectors);

        fixture.Scheduler.Unbind(otherSector.Id);
        fixture.Scheduler.Tick();

        Assert.Equal(2, fixture.Metrics.Current.SleepingBrains);
        Assert.Equal(1, fixture.Metrics.Current.SleepingSectors);
    }

    [Fact]
    public async Task StartAsync_RegistersOneSchedulerTimerAndStopAsyncCancelsIt()
    {
        var fixture = new SchedulerFixture(minTickMilliseconds: 125);

        await fixture.Scheduler.StartAsync();
        await fixture.Scheduler.StartAsync();

        Assert.True(fixture.Loop.Repeating.ContainsKey("npc-brain-scheduler"));
        Assert.Single(fixture.Loop.Repeating);
        Assert.Equal(TimeSpan.FromMilliseconds(125), fixture.Loop.RepeatingInterval);
        Assert.Null(fixture.Loop.RepeatingDelay);

        await fixture.Scheduler.StopAsync();
        Assert.Empty(fixture.Loop.Repeating);
    }

    private static NpcBrainEvent Event(NpcBrainEventType type, uint subject)
        => new(type, Snapshot(subject));

    private static int FaultLogCount(NpcBrainScheduler scheduler)
    {
        var field = typeof(NpcBrainScheduler).GetField(
            "_faultLogs",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        ) ?? throw new InvalidOperationException("Scheduler fault throttle storage was not found.");
        var entries = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            field.GetValue(scheduler)
        );

        return entries.Count;
    }

    private static IReadOnlyList<LogEvent> SchedulerWarnings(GlobalSerilogCapture logs)
        => logs.Events
            .Where(logEvent =>
                logEvent.Level == LogEventLevel.Warning &&
                logEvent.MessageTemplate.Text.StartsWith(
                    "NPC brain scheduler fault",
                    StringComparison.Ordinal
                )
            )
            .ToArray();

    private static BrainMobileSnapshot Snapshot(uint serial)
        => new(
            new Serial(serial),
            $"mobile-{serial}",
            false,
            0,
            new Point3D(10, 10, 0),
            10,
            10,
            false,
            Serial.Zero,
            false,
            0
        );

    private sealed class SchedulerFixture
    {
        public FakePersistenceService Persistence { get; } = new();

        public RecordingNpcBrainRuntime Runtime { get; } = new();

        public RecordingBrainIntentExecutor Intents { get; } = new();

        public StubSectorActivityService Sectors { get; } = new();

        public StubSessionManager Sessions { get; } = new();

        public MutableTimeProvider Time { get; } = new(
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero)
        );

        public NpcAiMetrics Metrics { get; } = new();

        public StubGameLoopContext Loop { get; } = new();

        public SpatialIndexService Spatial { get; }

        public NpcBrainScheduler Scheduler { get; }

        public SchedulerFixture(
            int maxBrainsPerLoop = 100,
            int maxEventsPerWake = 16,
            int minTickMilliseconds = 100,
            int maxTickMilliseconds = 60_000
        )
        {
            Runtime.Descriptors["guard"] = new("guard", 1000, 10, 15);
            Spatial = new(Persistence, new StubLoopAffinity(), new StubEventBus());
            var config = new MoongateConfig
            {
                NpcAi = new()
                {
                    Advanced = new()
                    {
                        MinTickMilliseconds = minTickMilliseconds,
                        MaxTickMilliseconds = maxTickMilliseconds,
                        MaxBrainsPerLoop = maxBrainsPerLoop,
                        MaxEventsPerBrainWake = maxEventsPerWake,
                        MaxMailboxEvents = Math.Max(16, maxEventsPerWake)
                    }
                }
            };
            var contextFactory = new NpcBrainContextFactory(Spatial, Sessions, Time);
            Scheduler = new(
                Loop,
                Runtime,
                Intents,
                Sectors,
                Persistence,
                contextFactory,
                Time,
                config,
                Metrics
            );
        }

        public MobileEntity AddMobile(
            uint serial,
            string brainId = "guard",
            int mapId = 0,
            int x = 10,
            int y = 10
        )
        {
            var mobile = new MobileEntity
            {
                Id = new Serial(serial),
                Name = $"mobile-{serial}",
                BrainScriptId = brainId,
                MapId = mapId,
                Position = new Point3D(x, y, 0),
                Hits = 10,
                HitsMax = 10
            };
            Persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            Spatial.AddOrUpdate(mobile);

            return mobile;
        }

        public MobileEntity AddActiveMobile(uint serial)
        {
            var mobile = AddMobile(serial);
            Sectors.SetActive(mobile, true);

            return mobile;
        }

        public void Move(MobileEntity mobile, int x, int y)
        {
            mobile.Position = new(x, y, mobile.Position.Z);
            Persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            Spatial.AddOrUpdate(mobile);
        }

        public void EnqueueSpeech(Serial ownerId, uint speakerId)
        {
            Scheduler.EnqueueEvent(
                ownerId,
                NpcBrainHookType.SpeechHeard,
                Event(NpcBrainEventType.SpeechHeard, speakerId)
            );
        }

        public int ThinkCount(Serial mobileId)
            => Runtime.Invocations.Count(
                invocation => invocation.MobileId == mobileId && invocation.Hook == NpcBrainHookType.Think
            );
    }

    private sealed class StubSectorActivityService : ISectorActivityService
    {
        private readonly HashSet<(int MapId, int SectorX, int SectorY)> _active = [];

        public SectorActivitySnapshot Current => new(_active.Count, 0);

        public bool IsActive(int mapId, int sectorX, int sectorY)
            => _active.Contains((mapId, sectorX, sectorY));

        public void SetActive(MobileEntity mobile, bool active)
        {
            var key = (mobile.MapId, mobile.Position.X >> 4, mobile.Position.Y >> 4);

            if (active)
            {
                _active.Add(key);
                return;
            }

            _active.Remove(key);
        }

        public void TrackPlayer(MobileEntity player)
        {
        }

        public void MovePlayer(Serial playerId, int mapId, int sectorX, int sectorY)
        {
        }

        public void UntrackPlayer(Serial playerId)
        {
        }

        public void Tick()
        {
        }
    }
}
