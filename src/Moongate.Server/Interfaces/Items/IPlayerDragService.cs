using Moongate.Server.Data.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Items;

/// <summary>
/// Stores pending drag state for player sessions during item pickup/drop flow.
/// </summary>
public interface IPlayerDragService
{
    /// <summary>
    /// Clears pending drag state for a session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    void Clear(long sessionId);

    /// <summary>
    /// Sets pending drag state for a session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="itemId">Dragged item identifier.</param>
    /// <param name="amount">Dragged amount.</param>
    /// <param name="sourceContainerId">Source container identifier.</param>
    /// <param name="sourceLocation">Source location.</param>
    void SetPending(
        long sessionId,
        Serial itemId,
        int amount,
        Serial sourceContainerId,
        Point3D sourceLocation
    );

    /// <summary>
    /// Tries to consume pending drag state for the specified item and session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="itemId">Expected dragged item identifier.</param>
    /// <param name="state">Resolved drag state.</param>
    /// <returns><c>true</c> when consumed; otherwise <c>false</c>.</returns>
    bool TryConsume(long sessionId, Serial itemId, out PlayerDragState state);

    /// <summary>
    /// Tries to get current pending drag state for a session.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="state">Resolved drag state.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGet(long sessionId, out PlayerDragState state);
}
