using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the tasks of the calendar. One timer of the timer wheel points at the nearest step of all the tasks; a
///     step is the occurrence of a task or, for a shutdown, one of its warnings before the stop.
/// </summary>
public sealed class ScheduleService : IScheduleService
{
    public const string TimerName = "schedule";

    private const int ShutdownNowMessage = 30016;
    private const int SecondsWarningMessage = 30017;
    private const int MinutesWarningMessage = 30224;
    private const int SnapSeconds = 5;
    private const int MinutesFromSeconds = 120;
    private const int SecondsPerMinute = 60;
    private const int MaxStepsPerTimer = 1000;

    private readonly Dictionary<string, DateTimeOffset> _cursor = new();
    private readonly HashSet<string> _warned = [];
    private readonly IDataLoaderService _data;
    private readonly ITimerService _timers;
    private readonly IBroadcastService _broadcast;
    private readonly IServerShutdownService _shutdown;
    private readonly IEventScriptService _scripts;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;
    private readonly ILocalizationService? _localization;
    private readonly ILogger _logger;

    private string? _timerId;
    private bool _stopRequested;

    public ScheduleService(
        IDataLoaderService data,
        ITimerService timers,
        IBroadcastService broadcast,
        IServerShutdownService shutdown,
        IEventScriptService scripts,
        TimeProvider time,
        ScheduleConfig config,
        ILocalizationService? localization = null,
        ILogger? logger = null
    ) : this(data, timers, broadcast, shutdown, scripts, time, config.Resolve(), localization, logger)
    {
    }

    internal ScheduleService(
        IDataLoaderService data,
        ITimerService timers,
        IBroadcastService broadcast,
        IServerShutdownService shutdown,
        IEventScriptService scripts,
        TimeProvider time,
        TimeZoneInfo zone,
        ILocalizationService? localization = null,
        ILogger? logger = null
    )
    {
        _data = data;
        _timers = timers;
        _broadcast = broadcast;
        _shutdown = shutdown;
        _scripts = scripts;
        _time = time;
        _zone = zone;
        _localization = localization;
        _logger = logger ?? Log.ForContext<ScheduleService>();
    }

    public IReadOnlyList<ScheduleTask> Tasks { get; private set; } = [];

    public Task StartAsync()
    {
        Tasks = _data.GetEntities<ScheduleFile>().SelectMany(file => file.Task).ToList();
        var now = _time.GetUtcNow();

        foreach (var task in Tasks)
        {
            _cursor[task.Id] = now;
        }

        Arm();
        _logger.Information("Scheduled {Count} tasks in {Zone}", Tasks.Count, _zone.Id);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Disarm();

        return Task.CompletedTask;
    }

    public DateTimeOffset? NextOccurrence(string taskId)
    {
        var task = Tasks.FirstOrDefault(candidate => candidate.Id == taskId);

        return task is null ? null : ScheduleCalendar.NextOccurrence(task.When, _time.GetUtcNow(), _zone);
    }

    private void Arm()
    {
        Disarm();

        if (Tasks.Count == 0)
        {
            return;
        }

        var next = Tasks.Select(task => NextStep(task, _cursor[task.Id])).MinBy(step => step.At);
        var delay = next.At - _time.GetUtcNow();

        if (delay < TimeSpan.FromMilliseconds(1))
        {
            delay = TimeSpan.FromMilliseconds(1);
        }

        try
        {
            _timerId = _timers.RegisterTimer(TimerName, delay, OnTimer);
        }
        catch (Exception exception)
        {
            // A callback that throws would stop the game loop: the schedule waits for the next restart instead.
            _logger.Error(exception, "The schedule could not arm its timer and stops until a restart");
        }
    }

    private void Disarm()
    {
        if (_timerId is null)
        {
            return;
        }

        _timers.UnregisterTimer(_timerId);
        _timerId = null;
    }

    private ScheduleStep NextStep(ScheduleTask task, DateTimeOffset cursor)
    {
        var occurrence = ScheduleCalendar.NextOccurrence(task.When, cursor, _zone);
        var best = new ScheduleStep(occurrence, 0, occurrence);

        if (!string.Equals(task.Action, "shutdown", StringComparison.Ordinal))
        {
            return best;
        }

        foreach (var warning in task.Warnings)
        {
            var at = occurrence - TimeSpan.FromSeconds(warning);

            if (at > cursor && at < best.At)
            {
                best = new(at, warning, occurrence);
            }
        }

        return best;
    }

    private void OnTimer()
    {
        _timerId = null;
        var now = _time.GetUtcNow();

        foreach (var task in Tasks)
        {
            for (var guard = 0; guard < MaxStepsPerTimer; guard++)
            {
                var step = NextStep(task, _cursor[task.Id]);

                if (step.At > now)
                {
                    break;
                }

                Run(task, step, now);
                _cursor[task.Id] = step.At;
            }
        }

        Arm();
    }

    private void Run(ScheduleTask task, ScheduleStep step, DateTimeOffset now)
    {
        try
        {
            switch (task.Action)
            {
                case "shutdown":
                    RunShutdown(task, step, now);

                    break;
                case "broadcast":
                    Broadcast(
                        task.Message is { } id ? _localization.Text(id, task.Text ?? $"Message {id}") : task.Text ?? ""
                    );

                    break;
                case "lua":
                    RunLua(task, step);

                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Scheduled task {Task} failed", task.Id);
        }
    }

    private void RunShutdown(ScheduleTask task, ScheduleStep step, DateTimeOffset now)
    {
        if (step.Offset > 0)
        {
            Broadcast(WarningText(step, now));

            return;
        }

        if (_stopRequested || _shutdown.Requested.IsCompleted)
        {
            _logger.Information("A shutdown is already pending: {Task} did not request another", task.Id);

            return;
        }

        _stopRequested = true;
        Broadcast(_localization.Text(ShutdownNowMessage, "The server is shutting down now."));
        _shutdown.RequestShutdown();
    }

    private string WarningText(ScheduleStep step, DateTimeOffset now)
    {
        var left = (int)Math.Round((step.Occurrence - now).TotalSeconds);

        if (Math.Abs(left - step.Offset) <= SnapSeconds)
        {
            left = step.Offset;
        }

        left = Math.Max(1, left);

        return left >= MinutesFromSeconds && left % SecondsPerMinute == 0
            ? _localization.Text(MinutesWarningMessage, "The server will shut down in {0} minutes.", left / SecondsPerMinute)
            : _localization.Text(SecondsWarningMessage, "The server will shut down in {0} seconds.", left);
    }

    private void RunLua(ScheduleTask task, ScheduleStep step)
    {
        var function = task.Function ?? "run";
        var result = _scripts.Call(task.Script!, function, task.Id, step.Occurrence.ToUnixTimeSeconds());

        if (result.Kind != ScriptResultKind.Completed && _warned.Add(task.Id))
        {
            _logger.Warning(
                "Scheduled task {Task}: the call to {Function} of {Script} gave {Kind}",
                task.Id,
                function,
                task.Script,
                result.Kind
            );
        }
    }

    private void Broadcast(string text)
    {
        _ = SafeBroadcastAsync(text);
    }

    private async Task SafeBroadcastAsync(string text)
    {
        try
        {
            await _broadcast.BroadcastAsync(text);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Scheduled broadcast failed");
        }
    }

    private readonly record struct ScheduleStep(DateTimeOffset At, int Offset, DateTimeOffset Occurrence);
}
