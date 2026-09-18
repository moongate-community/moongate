using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Tests.TestSupport.Diagnostics;

namespace Moongate.Tests.Server.Services.Diagnostics.Providers;

public sealed class TimerMetricsProviderTests
{
    [Fact]
    public async Task CollectAsync_MapsOneSnapshotToTimerMetrics()
    {
        var source = new TimerMetricsSourceStub(new TimerMetricsSnapshot
        {
            ActiveTimers = 4,
            RegisteredTimers = 15,
            ExecutedCallbacks = 12,
            CallbackFaults = 2,
            CoalescedOccurrences = 3,
            MaxLateness = TimeSpan.FromMilliseconds(250),
            MaxCallbackDuration = TimeSpan.FromMilliseconds(40),
            LastBatchDuration = TimeSpan.FromMilliseconds(18)
        });
        var provider = new TimerMetricsProvider(source);

        var metrics = await provider.CollectAsync();

        Assert.Equal("timers", provider.ProviderName);
        Assert.Equal(1, source.SnapshotReadCount);
        Assert.Collection(metrics,
            metric => AssertMetric(metric, "active_timers", 4d, "count", DiagnosticMetricType.Gauge),
            metric => AssertMetric(metric, "registered_timers_total", 15d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "executed_callbacks_total", 12d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "callback_faults_total", 2d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "coalesced_occurrences_total", 3d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "max_lateness_seconds", 0.25d, "seconds", DiagnosticMetricType.Gauge),
            metric => AssertMetric(metric, "max_callback_duration_seconds", 0.04d, "seconds", DiagnosticMetricType.Gauge),
            metric => AssertMetric(metric, "last_batch_duration_seconds", 0.018d, "seconds", DiagnosticMetricType.Gauge));
    }

    [Fact]
    public async Task CollectAsync_PreservesPreviouslyReturnedSnapshot()
    {
        var source = new TimerMetricsSourceStub(new TimerMetricsSnapshot { ActiveTimers = 4 });
        var provider = new TimerMetricsProvider(source);
        var first = await provider.CollectAsync();
        source.Snapshot = new TimerMetricsSnapshot { ActiveTimers = 10 };

        var second = await provider.CollectAsync();

        Assert.Equal(4d, first[0].Value);
        Assert.Equal(10d, second[0].Value);
    }

    [Fact]
    public async Task CollectAsync_ThrowsForPreCanceledTokenBeforeReadingSnapshot()
    {
        var source = new TimerMetricsSourceStub(new TimerMetricsSnapshot());
        var provider = new TimerMetricsProvider(source);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CollectAsync(cancellation.Token).AsTask());

        Assert.Equal(0, source.SnapshotReadCount);
    }

    private static void AssertMetric(MetricSample metric, string name, double value, string unit,
        DiagnosticMetricType type)
    {
        Assert.Equal(name, metric.Name);
        Assert.Equal(value, metric.Value);
        Assert.Equal(unit, metric.Unit);
        Assert.Equal(type, metric.Type);
    }
}
