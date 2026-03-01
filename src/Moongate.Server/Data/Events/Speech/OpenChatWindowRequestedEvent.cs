using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Speech;

/// <summary>
/// Event emitted when a client requests to open the chat window (0xB5).
/// </summary>
public readonly record struct OpenChatWindowRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    byte[] Payload
) : IGameEvent
{
    /// <summary>
    /// Creates an open-chat-window request event with current timestamp.
    /// </summary>
    public OpenChatWindowRequestedEvent(long sessionId, byte[] payload)
        : this(GameEventBase.CreateNow(), sessionId, payload) { }
}
