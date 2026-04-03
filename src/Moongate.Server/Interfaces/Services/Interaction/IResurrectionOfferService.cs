using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Tracks pending resurrection confirmations for connected player sessions.
/// </summary>
public interface IResurrectionOfferService
{
    /// <summary>
    /// Creates or replaces the pending resurrection offer for a session and opens the confirmation UI.
    /// </summary>
    /// <param name="sessionId">Target player session identifier.</param>
    /// <param name="characterId">Target player character identifier.</param>
    /// <param name="sourceType">Origin of the resurrection offer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the offer was created; otherwise <c>false</c>.</returns>
    Task<bool> TryCreateOfferAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates or replaces the pending resurrection offer for a session using explicit source context.
    /// </summary>
    /// <param name="sessionId">Target player session identifier.</param>
    /// <param name="characterId">Target player character identifier.</param>
    /// <param name="sourceType">Origin of the resurrection offer.</param>
    /// <param name="sourceSerial">Origin source serial identifier.</param>
    /// <param name="mapId">Map containing the offer source.</param>
    /// <param name="sourceLocation">Source location used for later proximity validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the offer was created; otherwise <c>false</c>.</returns>
    Task<bool> TryCreateOfferAsync(
        long sessionId,
        Serial characterId,
        ResurrectionOfferSourceType sourceType,
        Serial sourceSerial,
        int mapId,
        Point3D sourceLocation,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Accepts the pending resurrection offer for a session, if one exists and is still valid.
    /// </summary>
    /// <param name="sessionId">Target player session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when resurrection was applied; otherwise <c>false</c>.</returns>
    Task<bool> TryAcceptAsync(long sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines and removes any pending resurrection offer for a session.
    /// </summary>
    /// <param name="sessionId">Target player session identifier.</param>
    void Decline(long sessionId);
}
