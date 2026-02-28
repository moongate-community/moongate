using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Targeting;

/// <summary>
/// Event emitted when the client requests targeted spell cast (0xBF/0x2D).
/// </summary>
public readonly record struct TargetedSpellCastEvent(
    GameEventBase BaseEvent,
    long SessionId,
    ushort SpellId,
    Serial TargetSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a targeted spell cast event with current timestamp.
    /// </summary>
    public TargetedSpellCastEvent(long sessionId, ushort spellId, Serial targetSerial)
        : this(GameEventBase.CreateNow(), sessionId, spellId, targetSerial) { }
}
