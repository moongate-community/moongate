using Moongate.UO.Data.Maps;

namespace Moongate.Server.Interfaces.Services.Metrics;

/// <summary>
/// Provides a read-only snapshot of spatial-index runtime metrics.
/// </summary>
public interface ISpatialMetricsSource
{
    /// <summary>
    /// Gets the latest spatial index metrics snapshot.
    /// </summary>
    /// <returns>Spatial metrics snapshot.</returns>
    SectorSystemStats GetMetricsSnapshot();
}
