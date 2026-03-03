using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Provides access to decoration entries loaded from Data/decoration.
/// </summary>
public interface IDecorationDataService
{
    /// <summary>
    /// Replaces all currently loaded decoration entries.
    /// </summary>
    /// <param name="entries">Decoration entries.</param>
    void SetEntries(IReadOnlyList<DecorationEntry> entries);

    /// <summary>
    /// Returns all loaded decoration entries.
    /// </summary>
    /// <returns>All decoration entries.</returns>
    IReadOnlyList<DecorationEntry> GetAllEntries();

    /// <summary>
    /// Returns decoration entries for a map id.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <returns>Entries for that map.</returns>
    IReadOnlyList<DecorationEntry> GetEntriesByMap(int mapId);
}
