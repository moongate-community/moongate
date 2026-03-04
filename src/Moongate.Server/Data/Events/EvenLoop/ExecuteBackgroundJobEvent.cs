using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.EvenLoop;

/// <summary>
/// Event emitted when a background job execution is requested.
/// </summary>
public readonly record struct ExecuteBackgroundJobEvent(
    GameEventBase BaseEvent,
    string JobName
) : IGameEvent
{
    /// <summary>
    /// Creates the event with current timestamp.
    /// </summary>
    public ExecuteBackgroundJobEvent(string jobName)
        : this(GameEventBase.CreateNow(), jobName) { }
}
