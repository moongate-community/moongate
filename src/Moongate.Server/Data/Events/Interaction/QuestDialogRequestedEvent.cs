using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Interaction;

/// <summary>
/// Event emitted when a player selects the quest context menu entry for an NPC.
/// </summary>
public readonly record struct QuestDialogRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial TargetSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a quest dialog request event with current timestamp.
    /// </summary>
    public QuestDialogRequestedEvent(long sessionId, Serial targetSerial)
        : this(GameEventBase.CreateNow(), sessionId, targetSerial) { }
}
