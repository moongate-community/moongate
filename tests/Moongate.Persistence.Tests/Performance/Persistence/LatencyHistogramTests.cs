using Moongate.Persistence.Tests.TestSupport.Persistence.Stress;

namespace Moongate.Persistence.Tests.Performance.Persistence;

public sealed class LatencyHistogramTests
{
    [Fact]
    public void Percentiles_ConcurrentSamples_UseNearestRankWithoutLosingObservations()
    {
        var histogram = new LatencyHistogram();
        Parallel.For(1, 101, value => histogram.Record(value));
        Assert.Equal(100, histogram.Count);
        Assert.Equal(50, histogram.Percentile(0.50));
        Assert.Equal(95, histogram.Percentile(0.95));
        Assert.Equal(99, histogram.Percentile(0.99));
    }

    [Fact]
    public void Percentiles_EmptyAndOverflow_DoNotHideLongOperations()
    {
        var histogram = new LatencyHistogram();
        Assert.Equal(0, histogram.Percentile(0.99));
        histogram.Record(120_000);
        Assert.Equal(120_000, histogram.Percentile(0.99));
    }
}
