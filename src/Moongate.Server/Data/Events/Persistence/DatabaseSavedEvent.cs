using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.Persistence;

/// <summary>
/// Event emitted when a persistence save cycle has completed.
/// </summary>
public readonly record struct DatabaseSavedEvent(
    GameEventBase BaseEvent,
    double ElapsedMilliseconds
) : IGameEvent
{
    /// <summary>
    /// Creates the event with current timestamp.
    /// </summary>
    public DatabaseSavedEvent(double elapsedMilliseconds)
        : this(GameEventBase.CreateNow(), elapsedMilliseconds) { }

    /// <summary>
    /// Creates the event with explicit timestamp.
    /// </summary>
    public DatabaseSavedEvent(double elapsedMilliseconds, long timestamp)
        : this(new(timestamp), elapsedMilliseconds) { }
}
