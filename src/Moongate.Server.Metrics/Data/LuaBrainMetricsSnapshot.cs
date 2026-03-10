using Moongate.Server.Metrics.Data.Attributes;

namespace Moongate.Server.Metrics.Data;

/// <summary>
/// Immutable snapshot of Lua brain runner runtime metrics.
/// </summary>
public sealed class LuaBrainMetricsSnapshot
{
    public LuaBrainMetricsSnapshot(
        long dueBrainsTotal,
        long processedBrainsTotal,
        long deferredBrainsTotal,
        long processedTicksTotal,
        double tickDurationTotalMs,
        double tickDurationAvgMs,
        double tickDurationMaxMs
    )
    {
        DueBrainsTotal = dueBrainsTotal;
        ProcessedBrainsTotal = processedBrainsTotal;
        DeferredBrainsTotal = deferredBrainsTotal;
        ProcessedTicksTotal = processedTicksTotal;
        TickDurationTotalMs = tickDurationTotalMs;
        TickDurationAvgMs = tickDurationAvgMs;
        TickDurationMaxMs = tickDurationMaxMs;
    }

    [Metric("brains.due.total")]
    public long DueBrainsTotal { get; }

    [Metric("brains.processed.total")]
    public long ProcessedBrainsTotal { get; }

    [Metric("brains.deferred.total")]
    public long DeferredBrainsTotal { get; }

    [Metric("ticks.processed.total")]
    public long ProcessedTicksTotal { get; }

    [Metric("tick.duration.total_ms")]
    public double TickDurationTotalMs { get; }

    [Metric("tick.duration.avg_ms")]
    public double TickDurationAvgMs { get; }

    [Metric("tick.duration.max_ms")]
    public double TickDurationMaxMs { get; }
}
