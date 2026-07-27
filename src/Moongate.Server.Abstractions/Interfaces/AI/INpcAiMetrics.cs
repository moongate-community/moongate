using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Interfaces.AI;

/// <summary>Records thread-safe, process-local measurements for NPC brain processing.</summary>
public interface INpcAiMetrics
{
    /// <summary>Gets an immutable snapshot of the current measurements.</summary>
    NpcAiMetricsSnapshot Current { get; }

    /// <summary>Sets the current counts of active and grace-period sectors.</summary>
    void SetActiveSectorCounts(int active, int grace);

    /// <summary>Sets the current count of sleeping sectors.</summary>
    void SetSleepingSectorCount(int sleeping);

    /// <summary>Sets the current counts of active, sleeping and fault-backoff brains.</summary>
    void SetBrainCounts(int active, int sleeping, int faultBackoff);

    /// <summary>Records execution of a brain during a scheduler iteration.</summary>
    void RecordBrainExecuted();

    /// <summary>Records a brain deferred by scheduler limits.</summary>
    void RecordBrainDeferred();

    /// <summary>Records the duration of one brain hook invocation.</summary>
    void RecordHookInvocation(TimeSpan duration);

    /// <summary>Records a failed brain hook invocation.</summary>
    void RecordHookFailure(bool instructionBudgetExceeded);

    /// <summary>Records how a queued brain event was handled.</summary>
    void RecordEvent(NpcBrainEventDispositionType disposition);

    /// <summary>Records whether a brain intent was accepted for execution.</summary>
    void RecordIntent(bool accepted);

    /// <summary>Records whether a brain reload succeeded or used its fallback.</summary>
    void RecordReload(bool succeeded);
}
