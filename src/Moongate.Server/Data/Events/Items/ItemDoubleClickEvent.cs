using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Items;

/// <summary>
/// Event emitted when a client double-clicks an item serial.
/// </summary>
public readonly record struct ItemDoubleClickEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial ItemSerial
) : IGameEvent
{
    /// <summary>
    /// Creates an item double-click event with current timestamp.
    /// </summary>
    public ItemDoubleClickEvent(long sessionId, Serial itemSerial)
        : this(GameEventBase.CreateNow(), sessionId, itemSerial) { }
}
