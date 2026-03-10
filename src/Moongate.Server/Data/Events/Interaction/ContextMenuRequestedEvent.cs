using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Interaction;

/// <summary>
/// Event emitted when a client requests a context menu (0xBF/0x13).
/// </summary>
public readonly record struct ContextMenuRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial TargetSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a context menu request event with current timestamp.
    /// </summary>
    public ContextMenuRequestedEvent(long sessionId, Serial targetSerial)
        : this(GameEventBase.CreateNow(), sessionId, targetSerial) { }
}
