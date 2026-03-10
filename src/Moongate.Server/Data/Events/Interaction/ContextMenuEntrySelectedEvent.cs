using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Interaction;

/// <summary>
/// Event emitted when a client selects a context menu entry (0xBF/0x15).
/// </summary>
public readonly record struct ContextMenuEntrySelectedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial TargetSerial,
    ushort EntryTag
) : IGameEvent
{
    /// <summary>
    /// Creates a context menu selection event with current timestamp.
    /// </summary>
    public ContextMenuEntrySelectedEvent(long sessionId, Serial targetSerial, ushort entryTag)
        : this(GameEventBase.CreateNow(), sessionId, targetSerial, entryTag) { }
}
