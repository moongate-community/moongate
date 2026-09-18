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
            new MetricSample("active_timers", snapshot.ActiveTimers, "count", DiagnosticMetricType.Gauge),
            new MetricSample("registered_timers_total", snapshot.RegisteredTimers, "count",
                DiagnosticMetricType.Counter),
            new MetricSample("executed_callbacks_total", snapshot.ExecutedCallbacks, "count",
                DiagnosticMetricType.Counter),
            new MetricSample("callback_faults_total", snapshot.CallbackFaults, "count", DiagnosticMetricType.Counter),
            new MetricSample("coalesced_occurrences_total", snapshot.CoalescedOccurrences, "count",
                DiagnosticMetricType.Counter),
            new MetricSample("max_lateness_seconds", snapshot.MaxLateness.TotalSeconds, "seconds",
                DiagnosticMetricType.Gauge),
            new MetricSample("max_callback_duration_seconds", snapshot.MaxCallbackDuration.TotalSeconds, "seconds",
                DiagnosticMetricType.Gauge),
            new MetricSample("last_batch_duration_seconds", snapshot.LastBatchDuration.TotalSeconds, "seconds",
                DiagnosticMetricType.Gauge)
        ];
        return ValueTask.FromResult(samples);
    }
}
