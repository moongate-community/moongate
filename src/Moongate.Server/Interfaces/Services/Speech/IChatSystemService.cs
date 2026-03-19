using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Server.Data.Session;

namespace Moongate.Server.Interfaces.Services.Speech;

/// <summary>
/// Owns runtime conference chat state and packet-driven chat behavior.
/// </summary>
public interface IChatSystemService : IMoongateService
{
    /// <summary>
    /// Handles a parsed client chat action packet.
    /// </summary>
    Task HandleChatActionAsync(GameSession session, ChatTextPacket packet, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the chat window for the session and ensures the user exists in chat runtime state.
    /// </summary>
    Task OpenWindowAsync(GameSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any runtime chat state owned by the disconnected session.
    /// </summary>
    Task RemoveSessionAsync(long sessionId, CancellationToken cancellationToken = default);
}
