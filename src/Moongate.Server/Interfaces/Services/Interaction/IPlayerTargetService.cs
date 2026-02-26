using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates target cursor requests and responses for player sessions.
/// </summary>
public interface IPlayerTargetService : IMoongateService
{
    /// <summary>
    /// Sends a target cursor request to a session and registers the callback for the next cursor response.
    /// </summary>
    /// <param name="sessionId">Target network session id.</param>
    /// <param name="callback">Callback invoked when the client answers the target cursor.</param>
    /// <param name="selectionType">Requested cursor selection mode.</param>
    /// <param name="cursorType">Requested cursor semantic type.</param>
    /// <returns>Generated cursor id used to correlate the response.</returns>
    Task<Serial> SendTargetCursorAsync(
        long sessionId,
        Action<PendingCursorCallback> callback,
        TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
        TargetCursorType cursorType = TargetCursorType.Neutral
    );

    /// <summary>
    /// Cancels a pending target cursor for a session.
    /// </summary>
    /// <param name="sessionId">Target network session id.</param>
    /// <param name="cursorId">Cursor id to cancel.</param>
    Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId);
}
