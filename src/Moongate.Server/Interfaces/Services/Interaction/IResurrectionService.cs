using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Applies resurrection state changes to a dead player after a valid offer is accepted.
/// </summary>
public interface IResurrectionService
{
    /// <summary>
    /// Validates and applies resurrection for the character currently bound to the specified session.
    /// </summary>
    /// <param name="sessionId">Target player session identifier.</param>
    /// <param name="characterId">Expected player character identifier from the pending offer.</param>
    /// <param name="sourceType">Origin of the accepted resurrection offer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when resurrection was applied; otherwise <c>false</c>.</returns>
    Task<bool> TryResurrectAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates and applies resurrection for the character currently bound to the specified session using explicit source context.
    /// </summary>
    /// <param name="sessionId">Target player session identifier.</param>
    /// <param name="characterId">Expected player character identifier from the pending offer.</param>
    /// <param name="sourceType">Origin of the accepted resurrection offer.</param>
    /// <param name="sourceSerial">Origin source serial identifier.</param>
    /// <param name="mapId">Map containing the offer source.</param>
    /// <param name="sourceLocation">Source location used for proximity validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when resurrection was applied; otherwise <c>false</c>.</returns>
    Task<bool> TryResurrectAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        Serial sourceSerial,
        int mapId,
        Point3D sourceLocation,
        CancellationToken cancellationToken = default
    );
}
