using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Facade service for accessing static seed datasets loaded at startup.
/// </summary>
public interface ISeedDataService
{
    /// <summary>
    /// Returns all signs for a map.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <returns>Sign entries for the map.</returns>
    IReadOnlyList<SignEntry> GetSignsByMap(int mapId);

    /// <summary>
    /// Returns all decoration entries for a map.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <returns>Decoration entries for the map.</returns>
    IReadOnlyList<DecorationEntry> GetDecorationsByMap(int mapId);

    /// <summary>
    /// Returns all loaded door component entries.
    /// </summary>
    /// <returns>Door component entries.</returns>
    IReadOnlyList<DoorComponentEntry> GetDoors();

    /// <summary>
    /// Returns all known world locations.
    /// </summary>
    /// <returns>World location entries.</returns>
    IReadOnlyList<WorldLocationEntry> GetLocations();
}
