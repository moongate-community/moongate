using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Tests.TestSupport.Diagnostics;

namespace Moongate.Tests.Server.Services.Diagnostics.Providers;

public sealed class GameLoopMetricsProviderTests
{
    [Fact]
    public async Task CollectAsync_MapsOneSnapshotToGameLoopMetrics()
    {
        var source = new GameLoopMetricsSourceStub(new GameLoopMetricsSnapshot
        {
            QueueDepth = 3,
            OldestQueuedItemAge = TimeSpan.FromMilliseconds(450),
            AcceptedWorkItems = 11,
            RejectedWorkItems = 2,
            ExecutedWorkItems = 8,
            Faults = 1,
            LastBatchDuration = TimeSpan.FromMilliseconds(12),
            MaxHandlerDuration = TimeSpan.FromMilliseconds(75)
        });
        var provider = new GameLoopMetricsProvider(source);

        var metrics = await provider.CollectAsync();

        Assert.Equal("game_loop", provider.ProviderName);
        Assert.Equal(1, source.SnapshotReadCount);
        Assert.Collection(metrics,
            metric => AssertMetric(metric, "queue_depth", 3d, "count", DiagnosticMetricType.Gauge),
            metric => AssertMetric(metric, "oldest_queued_item_age_seconds", 0.45d, "seconds", DiagnosticMetricType.Gauge),
            metric => AssertMetric(metric, "accepted_work_items_total", 11d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "rejected_work_items_total", 2d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "executed_work_items_total", 8d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "faults_total", 1d, "count", DiagnosticMetricType.Counter),
            metric => AssertMetric(metric, "last_batch_duration_seconds", 0.012d, "seconds", DiagnosticMetricType.Gauge),
            metric => AssertMetric(metric, "max_handler_duration_seconds", 0.075d, "seconds", DiagnosticMetricType.Gauge));
    }

    [Fact]
    public async Task CollectAsync_PreservesPreviouslyReturnedSnapshot()
    {
        var source = new GameLoopMetricsSourceStub(new GameLoopMetricsSnapshot { QueueDepth = 3 });
        var provider = new GameLoopMetricsProvider(source);
        var first = await provider.CollectAsync();
        source.Snapshot = new GameLoopMetricsSnapshot { QueueDepth = 9 };

        var second = await provider.CollectAsync();

        Assert.Equal(3d, first[0].Value);
        Assert.Equal(9d, second[0].Value);
    }

    [Fact]
    public async Task CollectAsync_ThrowsForPreCanceledTokenBeforeReadingSnapshot()
    {
        var source = new GameLoopMetricsSourceStub(new GameLoopMetricsSnapshot());
        var provider = new GameLoopMetricsProvider(source);
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
