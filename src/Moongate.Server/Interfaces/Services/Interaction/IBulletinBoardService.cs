using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Coordinates classic bulletin board open, read, post, and remove flows over packet <c>0x71</c>.
/// </summary>
public interface IBulletinBoardService
{
    /// <summary>
    /// Opens the specified bulletin board and sends the initial board display plus message summaries.
    /// </summary>
    /// <param name="sessionId">Target session identifier.</param>
    /// <param name="boardId">Bulletin board item serial identifier.</param>
    /// <returns><see langword="true" /> when the board was opened; otherwise <see langword="false" />.</returns>
    Task<bool> OpenBoardAsync(long sessionId, Serial boardId);

    /// <summary>
    /// Persists a new top-level post or reply from the client bulletin board packet payload.
    /// </summary>
    /// <param name="session">Source session.</param>
    /// <param name="packet">Parsed bulletin board client packet.</param>
    /// <returns><see langword="true" /> when the post was accepted; otherwise <see langword="false" />.</returns>
    Task<bool> PostMessageAsync(GameSession session, BulletinBoardMessagesPacket packet);

    /// <summary>
    /// Removes a previously posted message when the session is allowed to delete it.
    /// </summary>
    /// <param name="session">Source session.</param>
    /// <param name="boardId">Bulletin board item serial identifier.</param>
    /// <param name="messageId">Bulletin board message serial identifier.</param>
    /// <returns><see langword="true" /> when the message was removed; otherwise <see langword="false" />.</returns>
    Task<bool> RemoveMessageAsync(GameSession session, uint boardId, uint messageId);

    /// <summary>
    /// Sends the full message body for the specified bulletin board message.
    /// </summary>
    /// <param name="session">Source session.</param>
    /// <param name="boardId">Bulletin board item serial identifier.</param>
    /// <param name="messageId">Bulletin board message serial identifier.</param>
    /// <returns><see langword="true" /> when the full message was sent; otherwise <see langword="false" />.</returns>
    Task<bool> SendMessageAsync(GameSession session, uint boardId, uint messageId);

    /// <summary>
    /// Sends a single message summary for the specified board message.
    /// </summary>
    /// <param name="session">Source session.</param>
    /// <param name="boardId">Bulletin board item serial identifier.</param>
    /// <param name="messageId">Bulletin board message serial identifier.</param>
    /// <returns><see langword="true" /> when the message summary was sent; otherwise <see langword="false" />.</returns>
    Task<bool> SendSummaryAsync(GameSession session, uint boardId, uint messageId);
}
