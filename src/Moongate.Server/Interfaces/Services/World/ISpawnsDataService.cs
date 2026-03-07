using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Provides access to spawn definitions loaded from Data/spawns.
/// </summary>
public interface ISpawnsDataService
{
    /// <summary>
    /// Returns all loaded spawn definitions.
    /// </summary>
    /// <returns>All spawn definitions.</returns>
    IReadOnlyList<SpawnDefinitionEntry> GetAllEntries();

    /// <summary>
    /// Returns spawn definitions filtered by map id.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <returns>Spawn definitions for the requested map.</returns>
    IReadOnlyList<SpawnDefinitionEntry> GetEntriesByMap(int mapId);

    /// <summary>
    /// Replaces all currently loaded spawn definitions.
    /// </summary>
    /// <param name="entries">Spawn definitions.</param>
    void SetEntries(IReadOnlyList<SpawnDefinitionEntry> entries);
}
