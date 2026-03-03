using Moongate.Server.Data.World;

namespace Moongate.Server.Interfaces.Services.World;

/// <summary>
/// Provides access to sign entries loaded from signs.cfg.
/// </summary>
public interface ISignDataService
{
    /// <summary>
    /// Replaces all sign entries.
    /// </summary>
    /// <param name="entries">Sign entries.</param>
    void SetEntries(IReadOnlyList<SignEntry> entries);

    /// <summary>
    /// Returns all sign entries.
    /// </summary>
    /// <returns>All sign entries.</returns>
    IReadOnlyList<SignEntry> GetAllEntries();

    /// <summary>
    /// Returns sign entries filtered by map id.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <returns>Sign entries for the requested map.</returns>
    IReadOnlyList<SignEntry> GetEntriesByMap(int mapId);
}
