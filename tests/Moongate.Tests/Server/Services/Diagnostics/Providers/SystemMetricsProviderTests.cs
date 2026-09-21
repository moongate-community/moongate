using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Data.Internal.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Tests.TestSupport.Diagnostics;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Services.Diagnostics.Providers;

public sealed class SystemMetricsProviderTests
{
    [Fact]
    public async Task CollectAsync_MapsProcessValuesAndDerivesCpuFromMonotonicElapsedTime()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow - TimeSpan.FromSeconds(10)));
        using var provider = new SystemMetricsProvider(clock, reader);

        var first = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.False(first.ContainsKey("cpu_usage_percent"));
        clock.Advance(TimeSpan.FromSeconds(2));
        reader.Reading = reader.Reading with { TotalProcessorTime = TimeSpan.FromSeconds(12) };

        var second = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.Equal(12, second.Count);
        AssertMetric(second, "process_id", 1234d, "count", DiagnosticMetricType.Gauge);
        AssertMetric(second, "uptime_seconds", 12d, "seconds", DiagnosticMetricType.Gauge);
        AssertMetric(second, "working_set_bytes", 111d, "bytes", DiagnosticMetricType.Gauge);
        AssertMetric(second, "private_memory_bytes", 222d, "bytes", DiagnosticMetricType.Gauge);
        AssertMetric(second, "managed_memory_bytes", 333d, "bytes", DiagnosticMetricType.Gauge);
        AssertMetric(second, "thread_count", 7d, "count", DiagnosticMetricType.Gauge);
        AssertMetric(second, "processor_count", 4d, "count", DiagnosticMetricType.Gauge);
        AssertMetric(second, "cpu_usage_percent", 25d, "percent", DiagnosticMetricType.Gauge);
        AssertMetric(second, "cpu_time_seconds_total", 12d, "seconds", DiagnosticMetricType.Counter);
        AssertMetric(second, "gc_gen0_collections_total", 4d, "count", DiagnosticMetricType.Counter);
        AssertMetric(second, "gc_gen1_collections_total", 5d, "count", DiagnosticMetricType.Counter);
        AssertMetric(second, "gc_gen2_collections_total", 6d, "count", DiagnosticMetricType.Counter);
    }

    [Fact]
    public async Task CollectAsync_DoesNotReduceUptimeWhenUtcClockMovesBackward()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow - TimeSpan.FromSeconds(10)));
        using var provider = new SystemMetricsProvider(clock, reader);
        await provider.CollectAsync();
        clock.UtcNow -= TimeSpan.FromDays(1);
        clock.Advance(TimeSpan.FromSeconds(2));

        var metrics = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.Equal(12d, metrics["uptime_seconds"].Value);
    }

    [Fact]
    public async Task CollectAsync_ClampsInitialUptimeToZeroForFutureProcessStart()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow + TimeSpan.FromMinutes(1)));
        using var provider = new SystemMetricsProvider(clock, reader);

        var metrics = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.Equal(0d, metrics["uptime_seconds"].Value);
    }

    [Fact]
    public async Task CollectAsync_ResetsCpuBaselineWhenElapsedTimeDoesNotAdvance()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow));
        using var provider = new SystemMetricsProvider(clock, reader);
        await provider.CollectAsync();
        reader.Reading = reader.Reading with { TotalProcessorTime = TimeSpan.FromSeconds(12) };

        var zeroElapsed = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.False(zeroElapsed.ContainsKey("cpu_usage_percent"));
        clock.Advance(TimeSpan.FromSeconds(2));
        reader.Reading = reader.Reading with { TotalProcessorTime = TimeSpan.FromSeconds(14) };
        var recovered = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);
        Assert.Equal(25d, recovered["cpu_usage_percent"].Value);
    }

    [Fact]
    public async Task CollectAsync_ResetsCpuBaselineWhenCpuTimeRegresses()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow));
        using var provider = new SystemMetricsProvider(clock, reader);
        await provider.CollectAsync();
        clock.Advance(TimeSpan.FromSeconds(2));
        reader.Reading = reader.Reading with { TotalProcessorTime = TimeSpan.FromSeconds(8) };

        var regressed = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.False(regressed.ContainsKey("cpu_usage_percent"));
        clock.Advance(TimeSpan.FromSeconds(2));
        reader.Reading = reader.Reading with { TotalProcessorTime = TimeSpan.FromSeconds(10) };
        var recovered = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);
        Assert.Equal(25d, recovered["cpu_usage_percent"].Value);
    }

    [Fact]
    public async Task CollectAsync_ClampsCpuUsageToLogicalCapacity()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow));
        using var provider = new SystemMetricsProvider(clock, reader);
        await provider.CollectAsync();
        clock.Advance(TimeSpan.FromSeconds(1));
        reader.Reading = reader.Reading with { TotalProcessorTime = TimeSpan.FromSeconds(20) };

        var metrics = (await provider.CollectAsync()).ToDictionary(metric => metric.Name);

        Assert.Equal(100d, metrics["cpu_usage_percent"].Value);
    }

    [Fact]
    public async Task CollectAsync_ThrowsForPreCanceledTokenBeforeReadingProcess()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow));
        using var provider = new SystemMetricsProvider(clock, reader);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CollectAsync(cancellation.Token).AsTask());

        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task CollectAsync_DoesNotMutatePreviouslyReturnedSamples()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow));
        using var provider = new SystemMetricsProvider(clock, reader);
        var first = await provider.CollectAsync();
        reader.Reading = reader.Reading with { WorkingSetBytes = 999 };

        var second = await provider.CollectAsync();

        Assert.Equal(111d, first.Single(metric => metric.Name == "working_set_bytes").Value);
        Assert.Equal(999d, second.Single(metric => metric.Name == "working_set_bytes").Value);
    }

    [Fact]
    public void Dispose_ReleasesOwnedProcessReader()
    {
        var clock = new WorldSaveTimeProvider();
        var reader = new StubProcessMetricsReader(CreateReading(clock.UtcNow));
        var provider = new SystemMetricsProvider(clock, reader);

        provider.Dispose();
        provider.Dispose();

        Assert.True(reader.IsDisposed);
    }

    private static ProcessMetricsReading CreateReading(DateTimeOffset startedAtUtc)
    {
        return new ProcessMetricsReading
        {
            ProcessId = 1234,
            ProcessorCount = 4,
            StartedAtUtc = startedAtUtc,
            TotalProcessorTime = TimeSpan.FromSeconds(10),
            WorkingSetBytes = 111,
            PrivateMemoryBytes = 222,
            ManagedMemoryBytes = 333,
            ThreadCount = 7,
            GcGen0Collections = 4,
            GcGen1Collections = 5,
            GcGen2Collections = 6
        };
    }

    private static void AssertMetric(
        IReadOnlyDictionary<string, MetricSample> metrics,
        string name, double value, string unit, DiagnosticMetricType type
    )
    {
        var metric = metrics[name];
        Assert.Equal(value, metric.Value);
        Assert.Equal(unit, metric.Unit);
        Assert.Equal(type, metric.Type);
    }
}
