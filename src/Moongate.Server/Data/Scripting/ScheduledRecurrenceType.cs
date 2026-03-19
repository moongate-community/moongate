namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Supported recurrence types for Lua-authored scheduled events.
/// </summary>
public enum ScheduledRecurrenceType
{
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}
