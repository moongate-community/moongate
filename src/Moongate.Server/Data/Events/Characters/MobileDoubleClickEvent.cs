using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Characters;

/// <summary>
/// Event emitted when a client double-clicks a mobile serial.
/// </summary>
public readonly record struct MobileDoubleClickEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial MobileSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a mobile double-click event with current timestamp.
    /// </summary>
    public MobileDoubleClickEvent(long sessionId, Serial mobileSerial)
        : this(GameEventBase.CreateNow(), sessionId, mobileSerial) { }
}
