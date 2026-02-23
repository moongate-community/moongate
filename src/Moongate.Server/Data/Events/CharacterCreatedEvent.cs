using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct CharacterCreatedEvent(
    string CharacterName,
    Serial AccountId,
    Serial CharacterId,
    long Timestamp
) : IGameEvent;
