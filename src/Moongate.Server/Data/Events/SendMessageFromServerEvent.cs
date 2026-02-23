namespace Moongate.Server.Data.Events;

/// <summary>
/// Event emitted when the server sends a speech message to a specific session.
/// </summary>
public readonly record struct SendMessageFromServerEvent(
    long SessionId,
    string Text,
    short Hue,
    short Font,
    string Language,
    long Timestamp
) : IGameEvent;
