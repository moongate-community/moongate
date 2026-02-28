using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Targeting;

/// <summary>
/// Event emitted when the client requests targeted item use (0xBF/0x2C).
/// </summary>
public readonly record struct TargetedItemUseEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial ItemSerial,
    Serial TargetSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a targeted item use event with current timestamp.
    /// </summary>
    public TargetedItemUseEvent(long sessionId, Serial itemSerial, Serial targetSerial)
        : this(GameEventBase.CreateNow(), sessionId, itemSerial, targetSerial) { }
}
