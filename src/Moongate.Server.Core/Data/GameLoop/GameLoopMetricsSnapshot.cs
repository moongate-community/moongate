namespace Moongate.Server.Core.Data.GameLoop;

/// <summary>
///     A point-in-time view of loop admission, queue age and command execution.
/// </summary>
public sealed record GameLoopMetricsSnapshot
{
    public int QueueDepth { get; init; }
    public TimeSpan OldestQueuedItemAge { get; init; }
    public long AcceptedWorkItems { get; init; }
    public long RejectedWorkItems { get; init; }
    public long ExecutedWorkItems { get; init; }
    public long Faults { get; init; }
    public TimeSpan LastBatchDuration { get; init; }
    public TimeSpan MaxHandlerDuration { get; init; }
}
