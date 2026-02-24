using Moongate.Server.Metrics.Data.Attributes;

namespace Moongate.Server.Metrics.Data;

/// <summary>
/// Immutable snapshot of spatial index metrics.
/// </summary>
public sealed class SpatialMetricsSnapshot
{
    public SpatialMetricsSnapshot(int totalSectors, int totalEntities, int maxEntitiesPerSector, double averageEntitiesPerSector)
    {
        TotalSectors = totalSectors;
        TotalEntities = totalEntities;
        MaxEntitiesPerSector = maxEntitiesPerSector;
        AverageEntitiesPerSector = averageEntitiesPerSector;
    }

    [Metric("sectors.total")]
    public int TotalSectors { get; }

    [Metric("entities.total")]
    public int TotalEntities { get; }

    [Metric("entities.per_sector.max")]
    public int MaxEntitiesPerSector { get; }

    [Metric("entities.per_sector.avg")]
    public double AverageEntitiesPerSector { get; }
}
