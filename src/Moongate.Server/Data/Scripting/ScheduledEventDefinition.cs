using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Registered Lua-authored scheduled event definition.
/// </summary>
public sealed class ScheduledEventDefinition
{
    public string EventId { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string TriggerName { get; set; } = string.Empty;

    public ScheduledRecurrenceType RecurrenceType { get; set; }

    public string? Time { get; set; }

    public string? TimeZone { get; set; }

    public string? StartAt { get; set; }

    public int? DayOfMonth { get; set; }

    public string[] DaysOfWeek { get; set; } = [];

    public Table? Payload { get; set; }

    public string? ScriptPath { get; set; }
}
