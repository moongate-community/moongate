using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Data.Events.Scheduling;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Timing;
using Serilog;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Loads Lua-authored scheduled event definitions and arms timer-wheel callbacks for them.
/// </summary>
public sealed class ScheduledEventService : IScheduledEventService
{
    private readonly ILogger _logger = Log.ForContext<ScheduledEventService>();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly IScheduledEventDefinitionService _definitionService;
    private readonly ITimerService _timerService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly Func<DateTime> _utcNow;
    private readonly Lock _sync = new();
    private readonly Dictionary<string, string> _timerIdsByEventId = new(StringComparer.OrdinalIgnoreCase);

    public ScheduledEventService(
        DirectoriesConfig directoriesConfig,
        IScriptEngineService scriptEngineService,
        IScheduledEventDefinitionService definitionService,
        ITimerService timerService,
        IGameEventBusService gameEventBusService,
        Func<DateTime>? utcNow = null
    )
    {
        _directoriesConfig = directoriesConfig;
        _scriptEngineService = scriptEngineService;
        _definitionService = definitionService;
        _timerService = timerService;
        _gameEventBusService = gameEventBusService;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public int GetScheduledEventCount()
    {
        lock (_sync)
        {
            return _timerIdsByEventId.Count;
        }
    }

    public Task StartAsync()
    {
        var eventsDirectory = GetEventsDirectory();

        if (!Directory.Exists(eventsDirectory))
        {
            _logger.Information("Scheduled events directory not found: {Path}", eventsDirectory);
            return Task.CompletedTask;
        }

        foreach (var scriptPath in Directory.EnumerateFiles(eventsDirectory, "*.lua", SearchOption.AllDirectories)
                                            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            _scriptEngineService.ExecuteScriptFile(scriptPath);
        }

        foreach (var definition in ResolveDefinitions(eventsDirectory))
        {
            if (!definition.Enabled)
            {
                continue;
            }

            ScheduleNextOccurrence(definition, _utcNow());
        }

        _logger.Information("ScheduledEventService started. LoadedEvents={Count}", _timerIdsByEventId.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        List<string> timerIds;

        lock (_sync)
        {
            timerIds = [.._timerIdsByEventId.Values];
            _timerIdsByEventId.Clear();
        }

        foreach (var timerId in timerIds)
        {
            _timerService.UnregisterTimer(timerId);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<ScheduledEventDefinition> ResolveDefinitions(string eventsDirectory)
    {
        foreach (var scriptPath in Directory.EnumerateFiles(eventsDirectory, "*.lua", SearchOption.AllDirectories)
                                            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(_directoriesConfig[DirectoryType.Scripts], scriptPath)
                                   .Replace('\\', '/');
            var eventId = Path.GetFileNameWithoutExtension(scriptPath);

            if (_definitionService.TryGet(eventId, out var definition) && definition is not null)
            {
                yield return definition;
                continue;
            }

            _logger.Warning(
                "Scheduled event script did not register a matching definition. EventId={EventId} ScriptPath={ScriptPath}",
                eventId,
                relativePath
            );
        }
    }

    private void ScheduleNextOccurrence(ScheduledEventDefinition definition, DateTime utcNow)
    {
        var nextOccurrenceUtc = ComputeNextOccurrenceUtc(definition, utcNow);

        if (nextOccurrenceUtc is null)
        {
            return;
        }

        var delay = nextOccurrenceUtc.Value - utcNow;

        if (delay <= TimeSpan.Zero)
        {
            delay = TimeSpan.FromMilliseconds(1);
        }

        var timerId = _timerService.RegisterTimer(
            $"scheduled_event:{definition.EventId}",
            delay,
            () => OnTimerFired(definition, nextOccurrenceUtc.Value),
            delay: delay,
            repeat: false
        );

        lock (_sync)
        {
            if (_timerIdsByEventId.TryGetValue(definition.EventId, out var existingTimerId))
            {
                _timerService.UnregisterTimer(existingTimerId);
            }

            _timerIdsByEventId[definition.EventId] = timerId;
        }
    }

    private void OnTimerFired(ScheduledEventDefinition definition, DateTime scheduledAtUtc)
    {
        lock (_sync)
        {
            _timerIdsByEventId.Remove(definition.EventId);
        }

        var firedAtUtc = _utcNow();
        var gameEvent = new ScheduledEventTriggeredEvent(
            definition.EventId,
            definition.TriggerName,
            scheduledAtUtc,
            firedAtUtc,
            definition.RecurrenceType,
            definition.Payload
        );

        _gameEventBusService.PublishAsync(gameEvent).AsTask().GetAwaiter().GetResult();

        if (definition.RecurrenceType != ScheduledRecurrenceType.Once && definition.Enabled)
        {
            ScheduleNextOccurrence(definition, firedAtUtc);
        }
    }

    private DateTime? ComputeNextOccurrenceUtc(ScheduledEventDefinition definition, DateTime utcNow)
    {
        return definition.RecurrenceType switch
        {
            ScheduledRecurrenceType.Once => ComputeOneShotOccurrence(definition, utcNow),
            ScheduledRecurrenceType.Daily => ComputeDailyOccurrence(definition, utcNow),
            ScheduledRecurrenceType.Weekly => ComputeWeeklyOccurrence(definition, utcNow),
            ScheduledRecurrenceType.Monthly => ComputeMonthlyOccurrence(definition, utcNow),
            _ => null
        };
    }

    private static DateTime? ComputeOneShotOccurrence(ScheduledEventDefinition definition, DateTime utcNow)
    {
        if (!DateTime.TryParse(
                definition.StartAt,
                null,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var startAtUtc))
        {
            return null;
        }

        return startAtUtc > utcNow ? startAtUtc : null;
    }

    private static DateTime? ComputeDailyOccurrence(ScheduledEventDefinition definition, DateTime utcNow)
    {
        var zone = ResolveTimeZone(definition.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
        var timeOfDay = ParseTimeOfDay(definition.Time);
        var candidateLocal = localNow.Date.Add(timeOfDay);

        if (candidateLocal <= localNow)
        {
            candidateLocal = candidateLocal.AddDays(1);
        }

        return ConvertLocalToUtc(candidateLocal, zone);
    }

    private static DateTime? ComputeWeeklyOccurrence(ScheduledEventDefinition definition, DateTime utcNow)
    {
        var zone = ResolveTimeZone(definition.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
        var timeOfDay = ParseTimeOfDay(definition.Time);
        var scheduledDays = definition.DaysOfWeek.Select(ParseDayOfWeek).Distinct().ToArray();

        if (scheduledDays.Length == 0)
        {
            return null;
        }

        for (var offset = 0; offset < 14; offset++)
        {
            var candidateDate = localNow.Date.AddDays(offset);

            if (!scheduledDays.Contains(candidateDate.DayOfWeek))
            {
                continue;
            }

            var candidateLocal = candidateDate.Add(timeOfDay);

            if (candidateLocal > localNow)
            {
                return ConvertLocalToUtc(candidateLocal, zone);
            }
        }

        return null;
    }

    private static DateTime? ComputeMonthlyOccurrence(ScheduledEventDefinition definition, DateTime utcNow)
    {
        if (definition.DayOfMonth is null)
        {
            return null;
        }

        var zone = ResolveTimeZone(definition.TimeZone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
        var timeOfDay = ParseTimeOfDay(definition.Time);
        var year = localNow.Year;
        var month = localNow.Month;

        for (var i = 0; i < 24; i++)
        {
            var candidateMonth = month + i;
            var candidateYear = year + ((candidateMonth - 1) / 12);
            var normalizedMonth = ((candidateMonth - 1) % 12) + 1;
            var maxDay = DateTime.DaysInMonth(candidateYear, normalizedMonth);
            var day = Math.Min(definition.DayOfMonth.Value, maxDay);
            var candidateLocal = new DateTime(candidateYear, normalizedMonth, day, 0, 0, 0, DateTimeKind.Unspecified)
                                 .Add(timeOfDay);

            if (candidateLocal > localNow)
            {
                return ConvertLocalToUtc(candidateLocal, zone);
            }
        }

        return null;
    }

    private string GetEventsDirectory()
        => Path.Combine(_directoriesConfig[DirectoryType.Scripts], "events");

    private static TimeSpan ParseTimeOfDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", null, out var parsed))
        {
            throw new InvalidOperationException($"Invalid scheduled event time value '{value}'.");
        }

        return parsed;
    }

    private static TimeZoneInfo ResolveTimeZone(string? value)
        => string.IsNullOrWhiteSpace(value) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(value.Trim());

    private static DateTime ConvertLocalToUtc(DateTime localTime, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), zone);

    private static DayOfWeek ParseDayOfWeek(string value)
        => value.Trim().ToLowerInvariant() switch
        {
            "sunday" => DayOfWeek.Sunday,
            "monday" => DayOfWeek.Monday,
            "tuesday" => DayOfWeek.Tuesday,
            "wednesday" => DayOfWeek.Wednesday,
            "thursday" => DayOfWeek.Thursday,
            "friday" => DayOfWeek.Friday,
            "saturday" => DayOfWeek.Saturday,
            _ => throw new InvalidOperationException($"Unsupported scheduled event day '{value}'.")
        };
}
