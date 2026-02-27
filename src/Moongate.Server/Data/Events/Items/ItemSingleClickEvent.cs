using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Items;

/// <summary>
/// Event emitted when a client single-clicks an item serial.
/// </summary>
public readonly record struct ItemSingleClickEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial ItemSerial
) : IGameEvent
{
    /// <summary>
    /// Creates an item single-click event with current timestamp.
    /// </summary>
    public ItemSingleClickEvent(long sessionId, Serial itemSerial)
        : this(GameEventBase.CreateNow(), sessionId, itemSerial) { }
}
