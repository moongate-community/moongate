using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Provides access to door component metadata loaded from data/components/doors.txt.
/// </summary>
public interface IDoorDataService
{
    /// <summary>
    /// Replaces all door component entries and rebuilds runtime toggle maps.
    /// </summary>
    /// <param name="entries">Door component entries.</param>
    void SetEntries(IReadOnlyList<DoorComponentEntry> entries);

    /// <summary>
    /// Returns all loaded door component entries.
    /// </summary>
    /// <returns>All entries.</returns>
    IReadOnlyList<DoorComponentEntry> GetAllEntries();

    /// <summary>
    /// Tries to resolve door toggle metadata for a concrete item id.
    /// </summary>
    /// <param name="itemId">Item id.</param>
    /// <param name="definition">Resolved toggle definition when found.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGetToggleDefinition(int itemId, out DoorToggleDefinition definition);
}
