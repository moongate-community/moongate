using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Interaction;

/// <summary>
/// Event emitted when a player requests the quest journal from the client quest button.
/// </summary>
public readonly record struct QuestJournalRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId
) : IGameEvent
{
    /// <summary>
    /// Creates a quest journal request event with current timestamp.
    /// </summary>
    public QuestJournalRequestedEvent(long sessionId)
        : this(GameEventBase.CreateNow(), sessionId) { }
}
