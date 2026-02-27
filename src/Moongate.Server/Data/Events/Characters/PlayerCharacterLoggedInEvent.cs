using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Event emitted when a player character has completed login initialization.
/// </summary>
public readonly record struct PlayerCharacterLoggedInEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial AccountId,
    Serial CharacterId
) : IGameEvent
{
    /// <summary>
    /// Creates a player-character logged-in event with current timestamp.
    /// </summary>
    public PlayerCharacterLoggedInEvent(long sessionId, Serial accountId, Serial characterId)
        : this(GameEventBase.CreateNow(), sessionId, accountId, characterId) { }

    /// <summary>
    /// Creates a player-character logged-in event with explicit timestamp.
    /// </summary>
    public PlayerCharacterLoggedInEvent(long sessionId, Serial accountId, Serial characterId, long timestamp)
        : this(new(timestamp), sessionId, accountId, characterId) { }
}
