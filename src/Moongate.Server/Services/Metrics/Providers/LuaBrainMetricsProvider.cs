using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Metrics.Data;

namespace Moongate.Server.Services.Metrics.Providers;

/// <summary>
/// Exposes Lua brain runner metrics.
/// </summary>
public sealed class LuaBrainMetricsProvider : IMetricProvider
{
    private readonly ILuaBrainMetricsSource _luaBrainMetricsSource;

    public LuaBrainMetricsProvider(ILuaBrainMetricsSource luaBrainMetricsSource)
    {
        _luaBrainMetricsSource = luaBrainMetricsSource;
    }

    public string ProviderName => "lua_brain";

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _luaBrainMetricsSource.GetMetricsSnapshot();

        return ValueTask.FromResult(snapshot.ToMetricSamples());
    }
}
