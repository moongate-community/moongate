using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Stores and serves map location entries loaded from JSON data.
/// </summary>
public interface ILocationCatalogService
{
    /// <summary>
    /// Returns all loaded location entries.
    /// </summary>
    /// <returns>Loaded locations.</returns>
    IReadOnlyList<WorldLocationEntry> GetAllLocations();

    /// <summary>
    /// Replaces all current location entries.
    /// </summary>
    /// <param name="locations">Locations to persist in memory.</param>
    void SetLocations(IReadOnlyList<WorldLocationEntry> locations);
}
