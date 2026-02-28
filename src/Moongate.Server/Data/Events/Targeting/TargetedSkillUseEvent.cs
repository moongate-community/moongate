using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Targeting;

/// <summary>
/// Event emitted when the client requests targeted skill use (0xBF/0x2E).
/// </summary>
public readonly record struct TargetedSkillUseEvent(
    GameEventBase BaseEvent,
    long SessionId,
    ushort SkillId,
    Serial TargetSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a targeted skill use event with current timestamp.
    /// </summary>
    public TargetedSkillUseEvent(long sessionId, ushort skillId, Serial targetSerial)
        : this(GameEventBase.CreateNow(), sessionId, skillId, targetSerial) { }
}
