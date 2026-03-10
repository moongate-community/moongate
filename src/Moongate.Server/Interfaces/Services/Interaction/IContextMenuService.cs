using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates context menu requests and selections.
/// </summary>
public interface IContextMenuService : IMoongateService
{
    /// <summary>
    /// Sends the context menu for a target entity to a session.
    /// </summary>
    /// <param name="sessionId">Target session id.</param>
    /// <param name="targetSerial">Target entity serial.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a menu was sent; otherwise <c>false</c>.</returns>
    Task<bool> SendContextMenuAsync(
        long sessionId,
        Serial targetSerial,
        CancellationToken cancellationToken = default
    );
}
