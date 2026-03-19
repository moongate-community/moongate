using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.World;

/// <summary>
/// In-memory store for teleporter definitions loaded at startup.
/// </summary>
public class TeleportersDataService : ITeleportersDataService
{
    private readonly object _sync = new();
    private List<TeleporterEntry> _entries = [];
    private Dictionary<int, List<TeleporterEntry>> _entriesBySourceMap = [];
    private Dictionary<(int MapId, int SectorX, int SectorY), List<TeleporterEntry>> _entriesBySourceSector = [];

    public IReadOnlyList<TeleporterEntry> GetAllEntries()
    {
        lock (_sync)
        {
            return [.. _entries];
        }
    }

    public IReadOnlyList<TeleporterEntry> GetEntriesBySourceMap(int mapId)
    {
        lock (_sync)
        {
            if (!_entriesBySourceMap.TryGetValue(mapId, out var entries))
            {
                return [];
            }

            return [.. entries];
        }
    }

    public IReadOnlyList<TeleporterEntry> GetEntriesBySourceSector(int mapId, int sectorX, int sectorY)
    {
        lock (_sync)
        {
            if (!_entriesBySourceSector.TryGetValue((mapId, sectorX, sectorY), out var entries))
            {
                return [];
            }

            return [.. entries];
        }
    }

    public void SetEntries(IReadOnlyList<TeleporterEntry> entries)
    {
        lock (_sync)
        {
            _entries = [.. entries];
            _entriesBySourceMap = entries.GroupBy(static entry => entry.SourceMapId)
                                         .ToDictionary(
                                             static grouping => grouping.Key,
                                             static grouping => grouping.ToList()
                                         );
            _entriesBySourceSector = entries.GroupBy(
                                                static entry => (
                                                                    entry.SourceMapId,
                                                                    entry.SourceLocation.X >> MapSectorConsts.SectorShift,
                                                                    entry.SourceLocation.Y >> MapSectorConsts.SectorShift
                                                                )
                                            )
                                            .ToDictionary(
                                                static grouping => grouping.Key,
                                                static grouping => grouping.ToList()
                                            );
        }
    }

    public bool TryGetEntryAtLocation(int mapId, Point3D location, out TeleporterEntry entry)
    {
        var sectorX = location.X >> MapSectorConsts.SectorShift;
        var sectorY = location.Y >> MapSectorConsts.SectorShift;
        var candidates = GetEntriesBySourceSector(mapId, sectorX, sectorY);

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];

            if (candidate.SourceLocation == location)
            {
                entry = candidate;

                return true;
            }
        }

        entry = default;

        return false;
    }

    public bool TryResolveTeleportDestination(
        int mapId,
        Point3D location,
        out int destinationMapId,
        out Point3D destinationLocation,
        int maxHops = 4
    )
    {
        var currentMapId = mapId;
        var currentLocation = location;

        for (var hop = 0; hop < Math.Max(1, maxHops); hop++)
        {
            if (!TryGetEntryAtLocation(currentMapId, currentLocation, out var entry))
            {
                destinationMapId = currentMapId;
                destinationLocation = currentLocation;

                return hop > 0;
            }

            currentMapId = entry.DestinationMapId;
            currentLocation = entry.DestinationLocation;
        }

        destinationMapId = currentMapId;
        destinationLocation = currentLocation;

        return true;
    }
}
