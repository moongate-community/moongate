namespace Moongate.Server.Abstractions.Data.AI;

public sealed record NpcAiMetricsSnapshot(
    long ActiveSectors,
    long GraceSectors,
    long SleepingSectors,
    long ActiveBrains,
    long SleepingBrains,
    long FaultBackoffBrains,
    long BrainsExecuted,
    long BrainsDeferred,
    long HookInvocations,
    long TotalHookDurationTicks,
    long MaxHookDurationTicks,
    long HookFailures,
    long InstructionBudgetBreaches,
    long EventsDelivered,
    long EventsCoalesced,
    long EventsDropped,
    long IntentsAccepted,
    long IntentsRejected,
    long ReloadSuccesses,
    long ReloadFallbacks
)
{
    public TimeSpan AverageHookDuration =>
        HookInvocations == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(TotalHookDurationTicks / HookInvocations);
}
