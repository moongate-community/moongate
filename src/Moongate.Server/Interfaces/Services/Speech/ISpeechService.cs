using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Interfaces.Services.Speech;

/// <summary>
/// Defines speech processing for incoming client speech packets.
/// </summary>
public interface ISpeechService
{
    /// <summary>
    /// Enqueues a server-origin message for all active sessions.
    /// </summary>
    /// <param name="text">Message text.</param>
    /// <param name="hue">Speech hue.</param>
    /// <param name="font">Speech font.</param>
    /// <param name="language">Language code.</param>
    /// <returns>Number of sessions that received the message.</returns>
    Task<int> BroadcastFromServerAsync(
        string text,
        short hue = SpeechHues.System,
        short font = SpeechHues.DefaultFont,
        string language = "ENU"
    );

    /// <summary>
    /// Processes an open-chat-window request packet.
    /// </summary>
    /// <param name="session">Source game session.</param>
    /// <param name="packet">Incoming open-chat-window packet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleOpenChatWindowAsync(
        GameSession session,
        OpenChatWindowPacket packet,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Processes an incoming Unicode speech packet for a session.
    /// </summary>
    /// <param name="session">Source game session.</param>
    /// <param name="speechPacket">Incoming speech packet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An outbound speech packet to enqueue when a chat message must be sent;
    /// otherwise <c>null</c>.
    /// </returns>
    Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
        GameSession session,
        UnicodeSpeechPacket speechPacket,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Enqueues a server-origin message for one specific session.
    /// </summary>
    /// <param name="session">Target session.</param>
    /// <param name="text">Message text.</param>
    /// <param name="hue">Speech hue.</param>
    /// <param name="font">Speech font.</param>
    /// <param name="language">Language code.</param>
    /// <returns>
    /// <c>true</c> when the message was enqueued; otherwise <c>false</c> (for invalid input).
    /// </returns>
    Task<bool> SendMessageFromServerAsync(
        GameSession session,
        string text,
        short hue = SpeechHues.System,
        short font = SpeechHues.DefaultFont,
        string language = "ENU"
    );
}
