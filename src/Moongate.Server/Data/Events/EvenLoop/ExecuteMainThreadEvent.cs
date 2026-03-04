using Moongate.Server.Data.Events.Base;

namespace Moongate.Server.Data.Events.EvenLoop;

/// <summary>
/// Event emitted when a callback is scheduled for game-loop thread execution.
/// </summary>
public readonly record struct ExecuteMainThreadEvent(
    GameEventBase BaseEvent,
    string ActionName
) : IGameEvent
{
    /// <summary>
    /// Creates the event with current timestamp.
    /// </summary>
    public ExecuteMainThreadEvent(string actionName)
        : this(GameEventBase.CreateNow(), actionName) { }
}
