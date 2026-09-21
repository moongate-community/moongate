using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Server.Services.Diagnostics.Providers;

public sealed class TimerMetricsProvider : IMetricProvider
{
    private readonly ITimerService _timers;

    public string ProviderName => "timers";

    public TimerMetricsProvider(ITimerService timers)
    {
        _timers = timers;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _timers.GetMetricsSnapshot();
        IReadOnlyList<MetricSample> samples =
        [
            new("active_timers", snapshot.ActiveTimers, "count", DiagnosticMetricType.Gauge),
            new(
                "registered_timers_total",
                snapshot.RegisteredTimers,
                "count",
                DiagnosticMetricType.Counter
            ),
            new(
                "executed_callbacks_total",
                snapshot.ExecutedCallbacks,
                "count",
                DiagnosticMetricType.Counter
            ),
            new("callback_faults_total", snapshot.CallbackFaults, "count", DiagnosticMetricType.Counter),
            new(
                "coalesced_occurrences_total",
                snapshot.CoalescedOccurrences,
                "count",
                DiagnosticMetricType.Counter
            ),
            new(
                "max_lateness_seconds",
                snapshot.MaxLateness.TotalSeconds,
                "seconds",
                DiagnosticMetricType.Gauge
            ),
            new(
                "max_callback_duration_seconds",
                snapshot.MaxCallbackDuration.TotalSeconds,
                "seconds",
                DiagnosticMetricType.Gauge
            ),
            new(
                "last_batch_duration_seconds",
                snapshot.LastBatchDuration.TotalSeconds,
                "seconds",
                DiagnosticMetricType.Gauge
            )
        ];

        return ValueTask.FromResult(samples);
    }
}
