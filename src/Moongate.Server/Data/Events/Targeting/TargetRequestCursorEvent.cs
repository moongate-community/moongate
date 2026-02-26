using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Internal.Cursors;

namespace Moongate.Server.Data.Events.Targeting;

/// <summary>
/// Event emitted when the server requests a target cursor for a session.
/// </summary>
public readonly record struct TargetRequestCursorEvent(
    GameEventBase BaseEvent,
    long SessionId,
    TargetCursorSelectionType SelectionType,
    TargetCursorType CursorType,
    Action<PendingCursorCallback> Callback
) : IGameEvent
{
    /// <summary>
    /// Creates a target request cursor event with current timestamp.
    /// </summary>
    public TargetRequestCursorEvent(
        long sessionId,
        TargetCursorSelectionType selectionType,
        TargetCursorType cursorType,
        Action<PendingCursorCallback> callback
    )
        : this(GameEventBase.CreateNow(), sessionId, selectionType, cursorType, callback) { }
}
