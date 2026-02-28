using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Event emitted when the client requests a stat lock change (0xBF/0x1A).
/// </summary>
public readonly record struct StatLockChangeRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Stat Stat,
    UOSkillLock LockState
) : IGameEvent
{
    /// <summary>
    /// Creates a stat lock change request event with current timestamp.
    /// </summary>
    public StatLockChangeRequestedEvent(long sessionId, Stat stat, UOSkillLock lockState)
        : this(GameEventBase.CreateNow(), sessionId, stat, lockState) { }
}
