namespace Moongate.Server.Data.Events;

/// <summary>
/// Event emitted when the server sends a speech message to a specific session.
/// </summary>
public readonly record struct SendMessageFromServerEvent(
    GameEventBase BaseEvent,
    long SessionId,
    string Text,
    short Hue,
    short Font,
    string Language
) : IGameEvent;
