using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Tests.Server.Core.Data.Diagnostics;

public sealed class DiagnosticSnapshotTests
{
    [Fact]
    public void Constructor_CopiesMutableMetricAndFailureSources()
    {
        var originalMetric = new MetricSample("cpu", 25d, "percent", DiagnosticMetricType.Gauge);
        var metrics = new Dictionary<string, MetricSample> { ["system.cpu"] = originalMetric };
        var failedProviders = new List<string> { "timer" };

        var snapshot = new DiagnosticSnapshot(7, DateTimeOffset.UnixEpoch,
            TimeSpan.FromMilliseconds(12), metrics, failedProviders);
        metrics["system.cpu"] = new MetricSample("cpu", 99d, "percent", DiagnosticMetricType.Gauge);
        metrics["system.memory"] = new MetricSample("memory", 512d, "bytes", DiagnosticMetricType.Gauge);
        failedProviders.Clear();

        Assert.Equal(7, snapshot.Sequence);
        Assert.Same(originalMetric, Assert.Single(snapshot.Metrics).Value);
        Assert.False(snapshot.Metrics.ContainsKey("system.memory"));
        Assert.Equal("timer", Assert.Single(snapshot.FailedProviders));
    }
}
