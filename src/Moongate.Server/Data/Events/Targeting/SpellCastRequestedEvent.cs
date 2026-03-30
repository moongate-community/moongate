using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Targeting;

/// <summary>
/// Event emitted when the client selects a spell to cast (0xBF/0x1C).
/// </summary>
public readonly record struct SpellCastRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    ushort SpellId
) : IGameEvent
{
    /// <summary>
    /// Creates a spell cast request event with current timestamp.
    /// </summary>
    public SpellCastRequestedEvent(long sessionId, ushort spellId)
        : this(GameEventBase.CreateNow(), sessionId, spellId) { }
}
