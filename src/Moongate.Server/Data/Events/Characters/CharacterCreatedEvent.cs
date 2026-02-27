using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct CharacterCreatedEvent(
    GameEventBase BaseEvent,
    string CharacterName,
    Serial AccountId,
    Serial CharacterId
) : IGameEvent
{
    /// <summary>
    /// Creates a character created event with current timestamp.
    /// </summary>
    public CharacterCreatedEvent(string characterName, Serial accountId, Serial characterId)
        : this(GameEventBase.CreateNow(), characterName, accountId, characterId) { }

    /// <summary>
    /// Creates a character created event with explicit timestamp.
    /// </summary>
    public CharacterCreatedEvent(string characterName, Serial accountId, Serial characterId, long timestamp)
        : this(new(timestamp), characterName, accountId, characterId) { }
}
