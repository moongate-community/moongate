using Moongate.Server.Interfaces.Services.Metrics;
using Moongate.UO.Data.Maps;

namespace Moongate.Tests.Server.Support;

public sealed class MetricsProvidersTestSpatialService : ISpatialMetricsSource
{
    public int TotalSectors { get; set; }

    public int TotalEntities { get; set; }

    public int MaxEntitiesPerSector { get; set; }

    public double AverageEntitiesPerSector { get; set; }

    public SectorSystemStats GetMetricsSnapshot()
        => new()
        {
            TotalSectors = TotalSectors,
            TotalEntities = TotalEntities,
            MaxEntitiesPerSector = MaxEntitiesPerSector,
            AverageEntitiesPerSector = AverageEntitiesPerSector
        };
}
