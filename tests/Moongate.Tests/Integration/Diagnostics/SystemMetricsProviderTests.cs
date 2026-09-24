using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;

namespace Moongate.Tests.Integration.Diagnostics;

public sealed class SystemMetricsProviderTests
{
    [Fact]
    public async Task CollectAsync_RealProcessReportsStableIdentityAndNonDecreasingCounters()
    {
        using var provider = new SystemMetricsProvider(TimeProvider.System);

        var first = ToDictionary(await provider.CollectAsync());
        await Task.Yield();
        var second = ToDictionary(await provider.CollectAsync());

        Assert.Equal(Environment.ProcessId, first["process_id"]);
        Assert.True(first["working_set_bytes"] >= 0);
        Assert.True(first["private_memory_bytes"] >= 0);
        Assert.True(first["managed_memory_bytes"] >= 0);
        Assert.True(second["cpu_time_seconds_total"] >= first["cpu_time_seconds_total"]);
        Assert.True(second["uptime_seconds"] >= first["uptime_seconds"]);
    }

    private static IReadOnlyDictionary<string, double> ToDictionary(IReadOnlyList<MetricSample> samples)
        => samples.ToDictionary(sample => sample.Name, sample => sample.Value);
}
