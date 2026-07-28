using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.AI;

namespace Moongate.Tests.Server.AI;

public class NpcAiMetricsTests
{
    [Fact]
    public void Current_ReflectsSectorAndBrainGauges()
    {
        var metrics = new NpcAiMetrics();

        metrics.SetActiveSectorCounts(3, 2);
        metrics.SetSleepingSectorCount(7);
        metrics.SetBrainCounts(11, 13, 17);

        var current = metrics.Current;

        Assert.Equal(3, current.ActiveSectors);
        Assert.Equal(2, current.GraceSectors);
        Assert.Equal(7, current.SleepingSectors);
        Assert.Equal(11, current.ActiveBrains);
        Assert.Equal(13, current.SleepingBrains);
        Assert.Equal(17, current.FaultBackoffBrains);
    }

    [Fact]
    public void RecordEvent_EachDispositionIncrementsItsCounter()
    {
        var metrics = new NpcAiMetrics();

        metrics.RecordEvent(NpcBrainEventDispositionType.Delivered);
        metrics.RecordEvent(NpcBrainEventDispositionType.Coalesced);
        metrics.RecordEvent(NpcBrainEventDispositionType.Dropped);

        var current = metrics.Current;

        Assert.Equal(1, current.EventsDelivered);
        Assert.Equal(1, current.EventsCoalesced);
        Assert.Equal(1, current.EventsDropped);
    }

    [Fact]
    public void RecordHookFailure_TracksBudgetBreachesSeparately()
    {
        var metrics = new NpcAiMetrics();

        metrics.RecordHookFailure(false);
        metrics.RecordHookFailure(true);

        var current = metrics.Current;

        Assert.Equal(2, current.HookFailures);
        Assert.Equal(1, current.InstructionBudgetBreaches);
    }

    [Fact]
    public void RecordHookInvocation_TracksCountTotalMaximumAndAverageDuration()
    {
        var metrics = new NpcAiMetrics();

        metrics.RecordHookInvocation(TimeSpan.FromTicks(10));
        metrics.RecordHookInvocation(TimeSpan.FromTicks(30));

        var current = metrics.Current;

        Assert.Equal(2, current.HookInvocations);
        Assert.Equal(40, current.TotalHookDurationTicks);
        Assert.Equal(30, current.MaxHookDurationTicks);
        Assert.Equal(TimeSpan.FromTicks(20), current.AverageHookDuration);
    }

    [Fact]
    public void RecordIntent_AcceptedAndRejectedIncrementSeparateCounters()
    {
        var metrics = new NpcAiMetrics();

        metrics.RecordIntent(true);
        metrics.RecordIntent(false);

        var current = metrics.Current;

        Assert.Equal(1, current.IntentsAccepted);
        Assert.Equal(1, current.IntentsRejected);
    }

    [Fact]
    public void RecordReload_SuccessAndFallbackIncrementSeparateCounters()
    {
        var metrics = new NpcAiMetrics();

        metrics.RecordReload(true);
        metrics.RecordReload(false);

        var current = metrics.Current;

        Assert.Equal(1, current.ReloadSuccesses);
        Assert.Equal(1, current.ReloadFallbacks);
    }
}
