using System.Text.RegularExpressions;
using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Reads
///     <c>
///         data/schedule.toml
///     </c>
///     : the timed tasks and the seasonal events. A file with anything wrong stops the startup
///     naming the entry and the field; no file is an empty calendar.
/// </summary>
public sealed partial class ScheduleLoader : IDataLoader<ScheduleFile>
{
    public const int MaxText = 200;
    public const int MaxWarning = 86400;

    private static readonly string[] ActionNames = ["shutdown", "broadcast", "lua"];
    private static readonly string[] EveryNames = ["hour", "day", "week"];
    private static readonly string[] DayNames = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

    private readonly ILogger _logger = Log.ForContext<ScheduleLoader>();
    private readonly DirectoriesConfig _directoriesConfig;

    private string schedulePath => Path.Join(_directoriesConfig["data"], "schedule.toml");

    public ScheduleLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<ScheduleFile>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(schedulePath))
        {
            _logger.Information("No schedule.toml: the calendar is empty");

            return new DataLoaderResult<ScheduleFile> { Entities = [] };
        }

        var file = await TomlUtils.DeserializeFromFileAsync<ScheduleFile>(schedulePath, null, cancellationToken) ??
                   new ScheduleFile();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in file.Task)
        {
            ValidateTask(task, ids);
        }

        foreach (var scheduled in file.Event)
        {
            ValidateEvent(scheduled, ids);
        }

        _logger.Information("Found {Tasks} scheduled tasks and {Events} events", file.Task.Count, file.Event.Count);

        return new DataLoaderResult<ScheduleFile> { Entities = [file] };
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant)]
    public static partial Regex IdPattern();

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.CultureInvariant)]
    private static partial Regex DayTime();

    [GeneratedRegex(@"^:[0-5]\d$", RegexOptions.CultureInvariant)]
    private static partial Regex HourMinute();

    [GeneratedRegex(@"^(\d{2})-(\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex MonthDay();

    [GeneratedRegex("^[a-z_][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FunctionName();

    private InvalidDataException Fail(string entry, string reason)
    {
        return new InvalidDataException($"{schedulePath}: {entry}: {reason}");
    }

    private void ValidateTask(ScheduleTask task, HashSet<string> ids)
    {
        var entry = $"task '{task.Id}'";

        if (!IdPattern().IsMatch(task.Id))
        {
            throw Fail(
                entry,
                "id must be lower case letters, digits and _, start with a letter and have at most 40 characters"
            );
        }

        if (!ids.Add(task.Id))
        {
            throw Fail(entry, "id is used twice");
        }

        ValidateWhen(entry, task.When);

        if (!ActionNames.Contains(task.Action))
        {
            throw Fail(entry, "action must be shutdown, broadcast or lua");
        }

        if (task.Action != "shutdown" && task.Warnings.Count > 0)
        {
            throw Fail(entry, "warnings are only for shutdown");
        }

        if (task.Action == "shutdown")
        {
            ValidateWarnings(entry, task.Warnings);
        }

        if (task.Action != "broadcast" && (task.Message is not null || task.Text is not null))
        {
            throw Fail(entry, task.Message is not null ? "message is only for broadcast" : "text is only for broadcast");
        }

        if (task.Action != "lua" && (task.Script is not null || task.Function is not null))
        {
            throw Fail(entry, task.Script is not null ? "script is only for lua" : "function is only for lua");
        }

        if (task.Action == "broadcast")
        {
            ValidateBroadcast(entry, task);
        }

        if (task.Action == "lua")
        {
            if (task.Script is null || !IdPattern().IsMatch(task.Script))
            {
                throw Fail(entry, "lua needs script, a file name under scripts/events like cleanup");
            }

            if (task.Function is not null && !FunctionName().IsMatch(task.Function))
            {
                throw Fail(entry, "function must be a name like run");
            }
        }
    }

    private void ValidateWhen(string entry, ScheduleWhen when)
    {
        if (!EveryNames.Contains(when.Every))
        {
            throw Fail(entry, "when.every must be hour, day or week");
        }

        if (when.Every == "hour" ? !HourMinute().IsMatch(when.At) : !DayTime().IsMatch(when.At))
        {
            throw Fail(
                entry,
                when.Every == "hour"
                    ? "when.at must be :MM for every hour"
                    : "when.at must be HH:MM (24 hours) for day and week"
            );
        }

        if (when.Days.Count > 0 && when.Every != "week")
        {
            throw Fail(entry, "when.days is only for every week");
        }

        if (when.Days.Any(day => !DayNames.Contains(day)) || when.Days.Distinct().Count() != when.Days.Count)
        {
            throw Fail(entry, "when.days holds mon, tue, wed, thu, fri, sat or sun, each once");
        }
    }

    private void ValidateWarnings(string entry, List<int> warnings)
    {
        var previous = int.MaxValue;

        foreach (var warning in warnings)
        {
            if (warning is < 1 or > MaxWarning || warning >= previous)
            {
                throw Fail(entry, $"warnings are whole seconds from 1 to {MaxWarning}, each smaller than the one before");
            }

            previous = warning;
        }
    }

    private void ValidateBroadcast(string entry, ScheduleTask task)
    {
        var hasMessage = task.Message is not null;
        var hasText = !string.IsNullOrWhiteSpace(task.Text);

        if (hasMessage == hasText || (task.Message is < 1) || task.Text is { Length: > MaxText })
        {
            throw Fail(
                entry,
                $"broadcast needs a message id (1 or more) or a text of at most {MaxText} characters, not both and not neither"
            );
        }
    }

    private void ValidateEvent(ScheduleEvent scheduled, HashSet<string> ids)
    {
        var entry = $"event '{scheduled.Id}'";

        if (!IdPattern().IsMatch(scheduled.Id))
        {
            throw Fail(
                entry,
                "id must be lower case letters, digits and _, start with a letter and have at most 40 characters"
            );
        }

        if (!ids.Add(scheduled.Id))
        {
            throw Fail(entry, "id is used twice");
        }

        if (string.IsNullOrWhiteSpace(scheduled.Name) || scheduled.Name.Length > 60)
        {
            throw Fail(entry, "name is 1 to 60 characters");
        }

        if (!IsMonthDay(scheduled.From))
        {
            throw Fail(entry, "from must be a real day written MM-dd, such as 10-20");
        }

        if (!IsMonthDay(scheduled.To))
        {
            throw Fail(entry, "to must be a real day written MM-dd, such as 11-02");
        }
    }

    // The year 2000 is a leap year: 02-29 is a day of a window.
    private static bool IsMonthDay(string text)
    {
        var match = MonthDay().Match(text);

        if (!match.Success)
        {
            return false;
        }

        var month = int.Parse(match.Groups[1].Value);
        var day = int.Parse(match.Groups[2].Value);

        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2000, month);
    }
}
