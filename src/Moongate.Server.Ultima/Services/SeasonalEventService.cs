using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the state of the seasonal events: a mode kept in the world props, the calendar of the window, and the
///     hooks called when an event starts or ends. A timer at every local midnight reads the calendar again.
/// </summary>
public sealed class SeasonalEventService : ISeasonalEventService
{
    public const string TimerName = "schedule.events";

    private const string KeyPrefix = "schedule.event.";
    private const string ModeAuto = "auto";
    private const string ModeOn = "on";
    private const string ModeOff = "off";

    private readonly Dictionary<string, bool> _active = new();
    private readonly HashSet<string> _warned = [];
    private readonly IDataLoaderService _data;
    private readonly IWorldPropsService _props;
    private readonly ITimerService _timers;
    private readonly IGameLoopService _loop;
    private readonly IEventScriptService _scripts;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;
    private readonly ILogger _logger = Log.ForContext<SeasonalEventService>();

    private List<ScheduleEvent> _events = [];
    private string? _timerId;

    public SeasonalEventService(
        IDataLoaderService data,
        IWorldPropsService props,
        ITimerService timers,
        IGameLoopService loop,
        IEventScriptService scripts,
        TimeProvider time,
        Data.Config.ScheduleConfig config
    ) : this(data, props, timers, loop, scripts, time, config.Resolve())
    {
    }

    internal SeasonalEventService(
        IDataLoaderService data,
        IWorldPropsService props,
        ITimerService timers,
        IGameLoopService loop,
        IEventScriptService scripts,
        TimeProvider time,
        TimeZoneInfo zone
    )
    {
        _loop = loop;
        _data = data;
        _props = props;
        _timers = timers;
        _scripts = scripts;
        _time = time;
        _zone = zone;
    }

    public IReadOnlyList<SeasonalEventState> Events
    {
        get { return _events.Select(Describe).ToList(); }
    }

    public async Task StartAsync()
    {
        _events = _data.GetEntities<ScheduleFile>().SelectMany(file => file.Event).ToList();

        // The props and the hooks belong to the game loop; startup runs off it.
        var start = new LoopActionWorkItem(() =>
            {
                Evaluate();
                Arm();
            }
        );
        await _loop.PostAsync(start);
        await start.Completion;
        _logger.Information("Loaded {Count} seasonal events", _events.Count);
    }

    public Task StopAsync()
    {
        if (_timerId is not null)
        {
            _timers.UnregisterTimer(_timerId);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public bool IsActive(string id)
    {
        return Get(id)?.Active ?? false;
    }

    public SeasonalEventState? Get(string id)
    {
        var item = _events.FirstOrDefault(candidate => candidate.Id == id);

        return item is null ? null : Describe(item);
    }

    public bool SetMode(string id, string mode)
    {
        if (_events.All(candidate => candidate.Id != id) || mode is not (ModeAuto or ModeOn or ModeOff))
        {
            return false;
        }

        _props.Set(ModeKey(id), mode == ModeAuto ? null : mode);
        Evaluate();

        return true;
    }

    private static string ModeKey(string id)
    {
        return $"{KeyPrefix}{id}.mode";
    }

    private static string ActiveKey(string id)
    {
        return $"{KeyPrefix}{id}.active";
    }

    private SeasonalEventState Describe(ScheduleEvent item)
    {
        return new(item.Id, item.Name, item.From, item.To, ModeOf(item), Computed(item));
    }

    private string ModeOf(ScheduleEvent item)
    {
        return _props.Get(ModeKey(item.Id)) is string mode and (ModeOn or ModeOff) ? mode : ModeAuto;
    }

    private bool Computed(ScheduleEvent item)
    {
        return ModeOf(item) switch
        {
            ModeOn => true,
            ModeOff => false,
            _ => ScheduleCalendar.IsInWindow(item.From, item.To, TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _zone).Date)
        };
    }

    private void Arm()
    {
        var delay = ScheduleCalendar.NextMidnight(_time.GetUtcNow(), _zone) - _time.GetUtcNow();

        if (delay < TimeSpan.FromMilliseconds(1))
        {
            delay = TimeSpan.FromMilliseconds(1);
        }

        try
        {
            _timerId = _timers.RegisterTimer(TimerName, delay, OnMidnight);
        }
        catch (Exception exception)
        {
            // A callback that throws would stop the game loop: the events wait for the next restart instead.
            _logger.Error(exception, "The seasonal events could not arm their timer and stay as they are until a restart");
        }
    }

    private void OnMidnight()
    {
        _timerId = null;
        Evaluate();
        Arm();
    }

    private void Evaluate()
    {
        foreach (var item in _events)
        {
            var active = Computed(item);
            var told = _props.Get(ActiveKey(item.Id)) is true;

            if (active == told)
            {
                continue;
            }

            _props.Set(ActiveKey(item.Id), active);
            CallHook(item, active ? "on_start" : "on_end");
        }
    }

    private void CallHook(ScheduleEvent item, string function)
    {
        // After the current work: not inside a running Lua script (a nested call is refused) and always on the loop.
        var work = new LoopActionWorkItem(() => RunHook(item, function));

        if (!_loop.TryPost(work))
        {
            _logger.Warning("The hook {Function} of the event {Event} could not be queued", function, item.Id);
        }
    }

    private void RunHook(ScheduleEvent item, string function)
    {
        try
        {
            var result = _scripts.Call(item.Id, function, item.Id, item.Name);

            if (result.Kind == ScriptResultKind.Failed && _warned.Add($"{item.Id}.{function}"))
            {
                _logger.Warning("The hook {Function} of the event {Event} failed", function, item.Id);
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "The hook {Function} of the event {Event} failed", function, item.Id);
        }
    }
}
