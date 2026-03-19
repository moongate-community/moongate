using System.Collections.Concurrent;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// In-memory registry of Lua-authored scheduled event definitions.
/// </summary>
public sealed class ScheduledEventDefinitionService : IScheduledEventDefinitionService
{
    private readonly ConcurrentDictionary<string, ScheduledEventDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Register(string eventId, Table? definition, string? scriptPath = null)
    {
        if (string.IsNullOrWhiteSpace(eventId) || definition is null)
        {
            return false;
        }

        var normalizedEventId = eventId.Trim();
        var parsed = ParseDefinition(normalizedEventId, definition, scriptPath);
        _definitions[normalizedEventId] = parsed;

        return true;
    }

    public bool TryGet(string eventId, out ScheduledEventDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        if (_definitions.TryGetValue(eventId.Trim(), out var resolved))
        {
            definition = resolved;
            return true;
        }

        return false;
    }

    private static ScheduledEventDefinition ParseDefinition(string eventId, Table definition, string? scriptPath)
    {
        var recurrenceText = RequireString(
            definition,
            "recurrence",
            $"Scheduled event '{eventId}' is missing 'recurrence'."
        );

        if (!Enum.TryParse<ScheduledRecurrenceType>(recurrenceText, true, out var recurrenceType))
        {
            throw new InvalidOperationException(
                $"Scheduled event '{eventId}' has unsupported recurrence '{recurrenceText}'."
            );
        }

        var parsed = new ScheduledEventDefinition
        {
            EventId = eventId,
            Enabled = ResolveOptionalBool(definition, "enabled") ?? true,
            TriggerName = RequireString(
                definition,
                "trigger_name",
                $"Scheduled event '{eventId}' is missing 'trigger_name'."
            ),
            RecurrenceType = recurrenceType,
            Time = ResolveOptionalString(definition, "time"),
            TimeZone = ResolveOptionalString(definition, "time_zone"),
            StartAt = ResolveOptionalString(definition, "start_at"),
            DayOfMonth = ResolveOptionalInt(definition, "day_of_month"),
            Payload = ResolveOptionalTable(definition, "payload"),
            ScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? null : scriptPath.Trim(),
            DaysOfWeek = ResolveStringArray(definition, "days_of_week")
        };

        Validate(parsed);

        return parsed;
    }

    private static void Validate(ScheduledEventDefinition definition)
    {
        switch (definition.RecurrenceType)
        {
            case ScheduledRecurrenceType.Once:
                if (string.IsNullOrWhiteSpace(definition.StartAt))
                {
                    throw new InvalidOperationException(
                        $"Scheduled event '{definition.EventId}' recurrence 'once' requires 'start_at'."
                    );
                }

                break;
            case ScheduledRecurrenceType.Daily:
                RequireTime(definition);
                break;
            case ScheduledRecurrenceType.Weekly:
                RequireTime(definition);

                if (definition.DaysOfWeek.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Scheduled event '{definition.EventId}' recurrence 'weekly' requires 'days_of_week'."
                    );
                }

                break;
            case ScheduledRecurrenceType.Monthly:
                RequireTime(definition);

                if (definition.DayOfMonth is null or < 1 or > 31)
                {
                    throw new InvalidOperationException(
                        $"Scheduled event '{definition.EventId}' recurrence 'monthly' requires 'day_of_month' between 1 and 31."
                    );
                }

                break;
        }
    }

    private static void RequireTime(ScheduledEventDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Time))
        {
            throw new InvalidOperationException(
                $"Scheduled event '{definition.EventId}' recurrence '{definition.RecurrenceType.ToString().ToLowerInvariant()}' requires 'time'."
            );
        }
    }

    private static string RequireString(Table table, string key, string message)
    {
        var value = table.Get(key);

        if (value.Type != DataType.String || string.IsNullOrWhiteSpace(value.String))
        {
            throw new InvalidOperationException(message);
        }

        return value.String.Trim();
    }

    private static string? ResolveOptionalString(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.String && !string.IsNullOrWhiteSpace(value.String) ? value.String.Trim() : null;
    }

    private static bool? ResolveOptionalBool(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.Boolean ? value.Boolean : null;
    }

    private static int? ResolveOptionalInt(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.Number ? (int)value.Number : null;
    }

    private static Table? ResolveOptionalTable(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.Table ? value.Table : null;
    }

    private static string[] ResolveStringArray(Table table, string key)
    {
        var value = table.Get(key);

        if (value.Type != DataType.Table || value.Table is null)
        {
            return [];
        }

        return value.Table.Pairs
                    .OrderBy(static pair => pair.Key.CastToNumber())
                    .Where(static pair => pair.Value.Type == DataType.String)
                    .Select(static pair => pair.Value.String?.Trim())
                    .Where(static day => !string.IsNullOrWhiteSpace(day))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
    }
}
