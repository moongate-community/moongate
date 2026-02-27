using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.Server.Metrics.Data;

namespace Moongate.Server.Services.Metrics.Providers;

/// <summary>
/// Exposes spatial index metrics.
/// </summary>
public sealed class SpatialMetricsProvider : IMetricProvider
{
    private readonly ISpatialMetricsSource _spatialMetricsSource;

    public SpatialMetricsProvider(ISpatialMetricsSource spatialMetricsSource)
    {
        _spatialMetricsSource = spatialMetricsSource;
    }

    public string ProviderName => "spatial";

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var stats = _spatialMetricsSource.GetMetricsSnapshot();
        var snapshot = new SpatialMetricsSnapshot(
            stats.TotalSectors,
            stats.TotalEntities,
            stats.MaxEntitiesPerSector,
            stats.AverageEntitiesPerSector
        );

        return ValueTask.FromResult(snapshot.ToMetricSamples());
    }
}
