using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Server.Services.Diagnostics.Providers;

public sealed class GameLoopMetricsProvider : IMetricProvider
{
    private readonly IGameLoopService _gameLoop;

    public string ProviderName => "game_loop";

    public GameLoopMetricsProvider(IGameLoopService gameLoop)
    {
        _gameLoop = gameLoop;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _gameLoop.GetMetricsSnapshot();
        IReadOnlyList<MetricSample> samples =
        [
            new("queue_depth", snapshot.QueueDepth, "count", DiagnosticMetricType.Gauge),
            new(
                "oldest_queued_item_age_seconds",
                snapshot.OldestQueuedItemAge.TotalSeconds,
                "seconds",
                DiagnosticMetricType.Gauge
            ),
            new(
                "accepted_work_items_total",
                snapshot.AcceptedWorkItems,
                "count",
                DiagnosticMetricType.Counter
            ),
            new(
                "rejected_work_items_total",
                snapshot.RejectedWorkItems,
                "count",
                DiagnosticMetricType.Counter
            ),
            new(
                "executed_work_items_total",
                snapshot.ExecutedWorkItems,
                "count",
                DiagnosticMetricType.Counter
            ),
            new("faults_total", snapshot.Faults, "count", DiagnosticMetricType.Counter),
            new(
                "last_batch_duration_seconds",
                snapshot.LastBatchDuration.TotalSeconds,
                "seconds",
                DiagnosticMetricType.Gauge
            ),
            new(
                "max_handler_duration_seconds",
                snapshot.MaxHandlerDuration.TotalSeconds,
                "seconds",
                DiagnosticMetricType.Gauge
            )
        ];

        return ValueTask.FromResult(samples);
    }
}
