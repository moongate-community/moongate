namespace Moongate.Server.Core.Data.Timing;

/// <summary>Timer registration counts and monotonic callback measurements.</summary>
public sealed class TimerMetricsSnapshot
{
    public int ActiveTimers { get; init; }
    public long RegisteredTimers { get; init; }
    public long ExecutedCallbacks { get; init; }
    public long CallbackFaults { get; init; }
    public long CoalescedOccurrences { get; init; }
    public TimeSpan MaxLateness { get; init; }
    public TimeSpan MaxCallbackDuration { get; init; }
    public TimeSpan LastBatchDuration { get; init; }
}
