using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Metrics.Data;

namespace Moongate.Tests.Server.Support;

public sealed class MetricsProvidersTestLuaBrainRunner : ILuaBrainMetricsSource
{
    public long DueBrainsTotal { get; set; }

    public long ProcessedBrainsTotal { get; set; }

    public long DeferredBrainsTotal { get; set; }

    public long ProcessedTicksTotal { get; set; }

    public double TickDurationTotalMs { get; set; }

    public double TickDurationAvgMs { get; set; }

    public double TickDurationMaxMs { get; set; }

    public LuaBrainMetricsSnapshot GetMetricsSnapshot()
        => new(
            DueBrainsTotal,
            ProcessedBrainsTotal,
            DeferredBrainsTotal,
            ProcessedTicksTotal,
            TickDurationTotalMs,
            TickDurationAvgMs,
            TickDurationMaxMs
        );
}
