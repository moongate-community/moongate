using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Speech;

/// <summary>
/// Event emitted when the server broadcasts a speech message to all active sessions.
/// </summary>
public readonly record struct BroadcastFromServerEvent(
    GameEventBase BaseEvent,
    string Text,
    short Hue,
    short Font,
    string Language,
    int RecipientCount
) : IGameEvent;
