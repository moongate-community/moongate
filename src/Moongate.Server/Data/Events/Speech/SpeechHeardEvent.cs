using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Events.Speech;

/// <summary>
/// Event emitted for a specific NPC when speech is heard in range.
/// </summary>
public readonly record struct SpeechHeardEvent(
    GameEventBase BaseEvent,
    Serial ListenerNpcId,
    Serial SpeakerId,
    string Text,
    ChatMessageType SpeechType,
    int MapId,
    Point3D Location
) : IGameEvent
{
    /// <summary>
    /// Creates a speech-heard event with current timestamp.
    /// </summary>
    public SpeechHeardEvent(
        Serial listenerNpcId,
        Serial speakerId,
        string text,
        ChatMessageType speechType,
        int mapId,
        Point3D location
    ) : this(GameEventBase.CreateNow(), listenerNpcId, speakerId, text, speechType, mapId, location) { }
}
