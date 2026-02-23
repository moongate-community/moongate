using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct CharacterSelectedEvent(
    long Sessionid,
    Serial CharacterId,
    long Timestamp
) : IGameEvent;
