using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Events.Speech;

/// <summary>
/// Event emitted when a mobile speech line is broadcast to nearby players.
/// </summary>
public readonly record struct MobileSpokeEvent(
    GameEventBase BaseEvent,
    Serial SpeakerId,
    int MapId,
    string Text,
    ChatMessageType MessageType,
    short Hue,
    short Font,
    string Language,
    int Range,
    int RecipientCount
) : IGameEvent
{
    /// <summary>
    /// Creates a mobile-spoke event with current timestamp.
    /// </summary>
    public MobileSpokeEvent(
        Serial speakerId,
        int mapId,
        string text,
        ChatMessageType messageType,
        short hue,
        short font,
        string language,
        int range,
        int recipientCount
    )
        : this(
            GameEventBase.CreateNow(),
            speakerId,
            mapId,
            text,
            messageType,
            hue,
            font,
            language,
            range,
            recipientCount
        ) { }
}
