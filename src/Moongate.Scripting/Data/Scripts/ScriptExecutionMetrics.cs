namespace Moongate.Scripting.Data.Scripts;

/// <summary>Counters since startup. Snapshot; not live.</summary>
public sealed record ScriptExecutionMetrics(
    int FilesLoaded,
    long CallsStarted,
    long CoroutinesResumed,
    long CoroutinesFinished,
    long Errors,
    long BudgetAborts,
    int ActiveCoroutines
);
