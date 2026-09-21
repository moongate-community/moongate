using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Tests.TestSupport.Diagnostics;

namespace Moongate.Tests.Server.Services.Diagnostics.Providers;

public sealed class SessionMetricsProviderTests
{
    [Fact]
    public async Task CollectAsync_MapsRegisteredSessionCountWithoutEnumeratingSessions()
    {
        var source = new SessionCountSourceStub(7);
        var provider = new SessionMetricsProvider(source);

        var metrics = await provider.CollectAsync();

        Assert.Equal("sessions", provider.ProviderName);
        Assert.Equal(1, source.CountReadCount);
        var metric = Assert.Single(metrics);
        AssertMetric(metric, "registered_sessions", 7d, "count", DiagnosticMetricType.Gauge);
    }

    [Fact]
    public async Task CollectAsync_PreservesPreviouslyReturnedCount()
    {
        var source = new SessionCountSourceStub(7);
        var provider = new SessionMetricsProvider(source);
        var first = await provider.CollectAsync();
        source.Count = 12;

        var second = await provider.CollectAsync();

        Assert.Equal(7d, first[0].Value);
        Assert.Equal(12d, second[0].Value);
    }

    [Fact]
    public async Task CollectAsync_ThrowsForPreCanceledTokenBeforeReadingCount()
    {
        var source = new SessionCountSourceStub(7);
        var provider = new SessionMetricsProvider(source);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.CollectAsync(cancellation.Token).AsTask());

        Assert.Equal(0, source.CountReadCount);
    }

    private static void AssertMetric(
        MetricSample metric,
        string name,
        double value,
        string unit,
        DiagnosticMetricType type
    )
    {
        Assert.Equal(name, metric.Name);
        Assert.Equal(value, metric.Value);
        Assert.Equal(unit, metric.Unit);
        Assert.Equal(type, metric.Type);
    }
}
