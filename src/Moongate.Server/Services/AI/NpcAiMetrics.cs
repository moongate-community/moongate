using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Services.AI;

public sealed class NpcAiMetrics : INpcAiMetrics
{
    private long _activeSectors;
    private long _graceSectors;
    private long _sleepingSectors;
    private long _activeBrains;
    private long _sleepingBrains;
    private long _faultBackoffBrains;
    private long _brainsExecuted;
    private long _brainsDeferred;
    private long _hookInvocations;
    private long _totalHookDurationTicks;
    private long _maxHookDurationTicks;
    private long _hookFailures;
    private long _instructionBudgetBreaches;
    private long _eventsDelivered;
    private long _eventsCoalesced;
    private long _eventsDropped;
    private long _intentsAccepted;
    private long _intentsRejected;
    private long _reloadSuccesses;
    private long _reloadFallbacks;

    public NpcAiMetricsSnapshot Current
        => new(
            Interlocked.Read(ref _activeSectors),
            Interlocked.Read(ref _graceSectors),
            Interlocked.Read(ref _sleepingSectors),
            Interlocked.Read(ref _activeBrains),
            Interlocked.Read(ref _sleepingBrains),
            Interlocked.Read(ref _faultBackoffBrains),
            Interlocked.Read(ref _brainsExecuted),
            Interlocked.Read(ref _brainsDeferred),
            Interlocked.Read(ref _hookInvocations),
            Interlocked.Read(ref _totalHookDurationTicks),
            Interlocked.Read(ref _maxHookDurationTicks),
            Interlocked.Read(ref _hookFailures),
            Interlocked.Read(ref _instructionBudgetBreaches),
            Interlocked.Read(ref _eventsDelivered),
            Interlocked.Read(ref _eventsCoalesced),
            Interlocked.Read(ref _eventsDropped),
            Interlocked.Read(ref _intentsAccepted),
            Interlocked.Read(ref _intentsRejected),
            Interlocked.Read(ref _reloadSuccesses),
            Interlocked.Read(ref _reloadFallbacks)
        );

    public void RecordBrainDeferred()
        => Interlocked.Increment(ref _brainsDeferred);

    public void RecordBrainExecuted()
        => Interlocked.Increment(ref _brainsExecuted);

    public void RecordEvent(NpcBrainEventDispositionType disposition)
    {
        switch (disposition)
        {
            case NpcBrainEventDispositionType.Delivered:
                Interlocked.Increment(ref _eventsDelivered);

                break;
            case NpcBrainEventDispositionType.Coalesced:
                Interlocked.Increment(ref _eventsCoalesced);

                break;
            case NpcBrainEventDispositionType.Dropped:
                Interlocked.Increment(ref _eventsDropped);

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null);
        }
    }

    public void RecordHookFailure(bool instructionBudgetExceeded)
    {
        Interlocked.Increment(ref _hookFailures);

        if (instructionBudgetExceeded)
        {
            Interlocked.Increment(ref _instructionBudgetBreaches);
        }
    }

    public void RecordHookInvocation(TimeSpan duration)
    {
        Interlocked.Increment(ref _hookInvocations);
        Interlocked.Add(ref _totalHookDurationTicks, duration.Ticks);
        UpdateMaximumHookDuration(duration.Ticks);
    }

    public void RecordIntent(bool accepted)
    {
        if (accepted)
        {
            Interlocked.Increment(ref _intentsAccepted);

            return;
        }

        Interlocked.Increment(ref _intentsRejected);
    }

    public void RecordReload(bool succeeded)
    {
        if (succeeded)
        {
            Interlocked.Increment(ref _reloadSuccesses);

            return;
        }

        Interlocked.Increment(ref _reloadFallbacks);
    }

    public void SetActiveSectorCounts(int active, int grace)
    {
        Interlocked.Exchange(ref _activeSectors, active);
        Interlocked.Exchange(ref _graceSectors, grace);
    }

    public void SetBrainCounts(int active, int sleeping, int faultBackoff)
    {
        Interlocked.Exchange(ref _activeBrains, active);
        Interlocked.Exchange(ref _sleepingBrains, sleeping);
        Interlocked.Exchange(ref _faultBackoffBrains, faultBackoff);
    }

    public void SetSleepingSectorCount(int sleeping)
        => Interlocked.Exchange(ref _sleepingSectors, sleeping);

    private void UpdateMaximumHookDuration(long durationTicks)
    {
        var currentMaximum = Interlocked.Read(ref _maxHookDurationTicks);

        while (durationTicks > currentMaximum)
        {
            var observedMaximum = Interlocked.CompareExchange(
                ref _maxHookDurationTicks,
                durationTicks,
                currentMaximum
            );

            if (observedMaximum == currentMaximum)
            {
                return;
            }

            currentMaximum = observedMaximum;
        }
    }
}
