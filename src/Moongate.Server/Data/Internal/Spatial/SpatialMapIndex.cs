using Moongate.UO.Data.Maps;

namespace Moongate.Server.Data.Internal.Spatial;

/// <summary>
/// Holds sector buckets for a single map id.
/// </summary>
public sealed class SpatialMapIndex
{
    private readonly Dictionary<(int X, int Y), MapSector> _sectors = new();

    /// <summary>
    /// Gets a snapshot of all sectors.
    /// </summary>
    public IReadOnlyCollection<MapSector> Sectors => _sectors.Values;

    /// <summary>
    /// Gets a sector by coordinates.
    /// </summary>
    /// <param name="sectorX">Sector x.</param>
    /// <param name="sectorY">Sector y.</param>
    /// <returns>Sector when found; otherwise <c>null</c>.</returns>
    public MapSector? GetSector(int sectorX, int sectorY)
    {
        _sectors.TryGetValue((sectorX, sectorY), out var sector);

        return sector;
    }

    /// <summary>
    /// Gets an existing sector or creates a new one.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="sectorX">Sector x.</param>
    /// <param name="sectorY">Sector y.</param>
    /// <returns>Sector bucket.</returns>
    public MapSector GetOrCreateSector(int mapId, int sectorX, int sectorY)
    {
        if (_sectors.TryGetValue((sectorX, sectorY), out var existing))
        {
            return existing;
        }

        var created = new MapSector(mapId, sectorX, sectorY);
        _sectors[(sectorX, sectorY)] = created;

        return created;
    }
}
