using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Defines lifecycle operations for persisted mobile entities.
/// </summary>
public interface IMobileService
{
    /// <summary>
    /// Inserts or updates a mobile entity in persistence storage.
    /// </summary>
    /// <param name="mobile">Mobile entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a mobile entity by serial identifier.
    /// </summary>
    /// <param name="id">Mobile serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when removed; otherwise <see langword="false" />.</returns>
    Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a mobile entity by serial identifier.
    /// </summary>
    /// <param name="id">Mobile serial identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mobile entity when found; otherwise <see langword="null" />.</returns>
    Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads persisted world mobiles (non-player) that belong to a specific sector.
    /// </summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="sectorX">Sector X coordinate.</param>
    /// <param name="sectorY">Sector Y coordinate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mobiles persisted in that sector.</returns>
    Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
        int mapId,
        int sectorX,
        int sectorY,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates and persists a mobile from a template at a specific world position.
    /// </summary>
    /// <param name="templateId">Mobile template identifier.</param>
    /// <param name="location">Spawn world position.</param>
    /// <param name="mapId">Spawn map identifier.</param>
    /// <param name="accountId">Optional owner account identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The spawned and persisted mobile entity.</returns>
    Task<UOMobileEntity> SpawnFromTemplateAsync(
        string templateId,
        Point3D location,
        int mapId,
        Serial? accountId = null,
        CancellationToken cancellationToken = default
    );
}
