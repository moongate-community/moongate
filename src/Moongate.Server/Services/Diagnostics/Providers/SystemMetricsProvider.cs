using System.Diagnostics;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Data.Internal.Diagnostics;
using Moongate.Server.Interfaces.Diagnostics.Internal;
using Moongate.Server.Services.Diagnostics.Internal;

namespace Moongate.Server.Services.Diagnostics.Providers;

public sealed class SystemMetricsProvider : IMetricProvider, IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly IProcessMetricsReader _reader;
    private bool _hasBaseline;
    private long _previousTimestamp;
    private TimeSpan _previousProcessorTime;
    private TimeSpan _uptime;

    public string ProviderName => "system";

    public SystemMetricsProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _reader = new ProcessMetricsReader(Process.GetCurrentProcess());
    }

    internal SystemMetricsProvider(TimeProvider timeProvider, IProcessMetricsReader reader)
    {
        _timeProvider = timeProvider;
        _reader = reader;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timestamp = _timeProvider.GetTimestamp();
        var reading = _reader.Read();
        double? cpuUsagePercent = null;

        if (_hasBaseline)
        {
            var elapsed = _timeProvider.GetElapsedTime(_previousTimestamp, timestamp);
            var processorDelta = reading.TotalProcessorTime - _previousProcessorTime;

            if (elapsed > TimeSpan.Zero)
            {
                _uptime += elapsed;

                if (processorDelta >= TimeSpan.Zero && reading.ProcessorCount > 0)
                {
                    cpuUsagePercent = Math.Clamp(
                        100d * processorDelta.TotalSeconds / (elapsed.TotalSeconds * reading.ProcessorCount),
                        0d,
                        100d
                    );
                }
            }
        }
        else
        {
            var initialUptime = _timeProvider.GetUtcNow() - reading.StartedAtUtc;
            _uptime = initialUptime > TimeSpan.Zero ? initialUptime : TimeSpan.Zero;
            _hasBaseline = true;
        }

        _previousTimestamp = timestamp;
        _previousProcessorTime = reading.TotalProcessorTime;

        var metrics = CreateMetrics(reading, cpuUsagePercent);

        return ValueTask.FromResult<IReadOnlyList<MetricSample>>(Array.AsReadOnly(metrics));
    }

    private MetricSample[] CreateMetrics(ProcessMetricsReading reading, double? cpuUsagePercent)
    {
        var metrics = new List<MetricSample>(cpuUsagePercent.HasValue ? 12 : 11)
        {
            new("process_id", reading.ProcessId, "count", DiagnosticMetricType.Gauge),
            new("uptime_seconds", _uptime.TotalSeconds, "seconds", DiagnosticMetricType.Gauge),
            new("working_set_bytes", reading.WorkingSetBytes, "bytes", DiagnosticMetricType.Gauge),
            new("private_memory_bytes", reading.PrivateMemoryBytes, "bytes", DiagnosticMetricType.Gauge),
            new("managed_memory_bytes", reading.ManagedMemoryBytes, "bytes", DiagnosticMetricType.Gauge),
            new("thread_count", reading.ThreadCount, "count", DiagnosticMetricType.Gauge),
            new("processor_count", reading.ProcessorCount, "count", DiagnosticMetricType.Gauge),
            new("cpu_time_seconds_total", reading.TotalProcessorTime.TotalSeconds, "seconds", DiagnosticMetricType.Counter),
            new("gc_gen0_collections_total", reading.GcGen0Collections, "count", DiagnosticMetricType.Counter),
            new("gc_gen1_collections_total", reading.GcGen1Collections, "count", DiagnosticMetricType.Counter),
            new("gc_gen2_collections_total", reading.GcGen2Collections, "count", DiagnosticMetricType.Counter)
        };

        if (cpuUsagePercent.HasValue)
        {
            metrics.Add(
                new(
                    "cpu_usage_percent",
                    cpuUsagePercent.Value,
                    "percent",
                    DiagnosticMetricType.Gauge
                )
            );
        }

        return metrics.ToArray();
    }

    public void Dispose()
        => _reader.Dispose();
}
