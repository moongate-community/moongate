using Moongate.UO.Data.Ids;
using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct CharacterSelectedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial CharacterId
) : IGameEvent
{
    /// <summary>
    /// Creates a character selected event with current timestamp.
    /// </summary>
    public CharacterSelectedEvent(long sessionId, Serial characterId)
        : this(GameEventBase.CreateNow(), sessionId, characterId) { }

    /// <summary>
    /// Creates a character selected event with explicit timestamp.
    /// </summary>
    public CharacterSelectedEvent(long sessionId, Serial characterId, long timestamp)
        : this(new GameEventBase(timestamp), sessionId, characterId) { }
}
