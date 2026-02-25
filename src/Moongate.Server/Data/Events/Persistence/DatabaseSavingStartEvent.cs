using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Persistence;

/// <summary>
/// Event emitted when a persistence save cycle starts.
/// </summary>
public readonly record struct DatabaseSavingStartEvent(GameEventBase BaseEvent) : IGameEvent
{
    /// <summary>
    /// Creates the event with current timestamp.
    /// </summary>
    public DatabaseSavingStartEvent()
        : this(GameEventBase.CreateNow()) { }

    /// <summary>
    /// Creates the event with explicit timestamp.
    /// </summary>
    public DatabaseSavingStartEvent(long timestamp)
        : this(new GameEventBase(timestamp)) { }
}
