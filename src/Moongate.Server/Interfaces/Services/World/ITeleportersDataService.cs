using Moongate.Server.Data.World;
using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Provides access to teleporter definitions loaded from data/teleporters/teleporters.json.
/// </summary>
public interface ITeleportersDataService
{
    /// <summary>
    /// Returns all loaded teleporter definitions.
    /// </summary>
    /// <returns>All teleporter definitions.</returns>
    IReadOnlyList<TeleporterEntry> GetAllEntries();

    /// <summary>
    /// Returns teleporter definitions filtered by source map id.
    /// </summary>
    /// <param name="mapId">Source map id.</param>
    /// <returns>Teleporter definitions for the requested source map.</returns>
    IReadOnlyList<TeleporterEntry> GetEntriesBySourceMap(int mapId);

    /// <summary>
    /// Returns teleporter definitions filtered by source map/sector.
    /// </summary>
    /// <param name="mapId">Source map id.</param>
    /// <param name="sectorX">Sector X.</param>
    /// <param name="sectorY">Sector Y.</param>
    /// <returns>Teleporter definitions in the requested source sector.</returns>
    IReadOnlyList<TeleporterEntry> GetEntriesBySourceSector(int mapId, int sectorX, int sectorY);

    /// <summary>
    /// Tries to resolve an exact source-location teleporter entry.
    /// </summary>
    /// <param name="mapId">Source map id.</param>
    /// <param name="location">Source location.</param>
    /// <param name="entry">Resolved teleporter entry when found.</param>
    /// <returns><c>true</c> when resolved; otherwise <c>false</c>.</returns>
    bool TryGetEntryAtLocation(int mapId, Point3D location, out TeleporterEntry entry);

    /// <summary>
    /// Tries to resolve teleporter destination starting from a source map/location.
    /// Supports chained teleporters up to <paramref name="maxHops"/> to prevent loops.
    /// </summary>
    /// <param name="mapId">Source map id.</param>
    /// <param name="location">Source location.</param>
    /// <param name="destinationMapId">Resolved destination map id.</param>
    /// <param name="destinationLocation">Resolved destination location.</param>
    /// <param name="maxHops">Maximum chained teleporter hops.</param>
    /// <returns><c>true</c> when a destination is resolved; otherwise <c>false</c>.</returns>
    bool TryResolveTeleportDestination(
        int mapId,
        Point3D location,
        out int destinationMapId,
        out Point3D destinationLocation,
        int maxHops = 4
    );

    /// <summary>
    /// Replaces all currently loaded teleporter definitions.
    /// </summary>
    /// <param name="entries">Teleporter definitions.</param>
    void SetEntries(IReadOnlyList<TeleporterEntry> entries);
}
