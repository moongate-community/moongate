using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Events.Scheduling;

/// <summary>
/// Raised when a scheduled event definition fires.
/// </summary>
public readonly record struct ScheduledEventTriggeredEvent(
    GameEventBase BaseEvent,
    string EventId,
    string TriggerName,
    DateTime ScheduledAtUtc,
    DateTime FiredAtUtc,
    ScheduledRecurrenceType RecurrenceType,
    Table? Payload
) : IGameEvent
{
    public ScheduledEventTriggeredEvent(
        string eventId,
        string triggerName,
        DateTime scheduledAtUtc,
        DateTime firedAtUtc,
        ScheduledRecurrenceType recurrenceType,
        Table? payload = null
    )
        : this(GameEventBase.CreateNow(), eventId, triggerName, scheduledAtUtc, firedAtUtc, recurrenceType, payload) { }
}
