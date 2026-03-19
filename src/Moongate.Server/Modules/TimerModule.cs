using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Timing;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Modules;

[ScriptModule("timer", "Provides timer registration APIs for scripts.")]
public sealed class TimerModule
{
    private readonly ILogger _logger = Log.ForContext<TimerModule>();
    private readonly ITimerService _timerService;

    public TimerModule(ITimerService timerService)
    {
        _timerService = timerService;
    }

    [ScriptFunction("after", "Registers a one-shot timer and returns its timer id.")]
    public string After(string name, int delayMs, Closure callback)
    {
        if (!IsValidRegistration(name, delayMs, callback))
        {
            return string.Empty;
        }

        return _timerService.RegisterTimer(
            name,
            TimeSpan.FromMilliseconds(delayMs),
            CreateSafeCallback(name, callback),
            TimeSpan.FromMilliseconds(delayMs)
        );
    }

    [ScriptFunction("cancel", "Cancels a timer by timer id.")]
    public bool Cancel(string timerId)
        => !string.IsNullOrWhiteSpace(timerId) && _timerService.UnregisterTimer(timerId);

    [ScriptFunction("cancel_all", "Cancels all registered timers.")]
    public void CancelAll()
        => _timerService.UnregisterAllTimers();

    [ScriptFunction("cancel_by_name", "Cancels all timers with the same logical name and returns removed count.")]
    public int CancelByName(string name)
        => string.IsNullOrWhiteSpace(name) ? 0 : _timerService.UnregisterTimersByName(name);

    [ScriptFunction("every", "Registers a repeating timer and returns its timer id.")]
    public string Every(string name, int intervalMs, Closure callback, int? delayMs = null)
    {
        if (!IsValidRegistration(name, intervalMs, callback))
        {
            return string.Empty;
        }

        var resolvedDelay = delayMs.HasValue
                                ? Math.Max(1, delayMs.Value)
                                : intervalMs;

        return _timerService.RegisterTimer(
            name,
            TimeSpan.FromMilliseconds(intervalMs),
            CreateSafeCallback(name, callback),
            TimeSpan.FromMilliseconds(resolvedDelay),
            true
        );
    }

    private Action CreateSafeCallback(string timerName, Closure callback)
        => () =>
           {
               try
               {
                   callback.OwnerScript.Call(callback);
               }
               catch (Exception ex)
               {
                   _logger.Error(ex, "Lua timer callback failed. TimerName={TimerName}", timerName);
               }
           };

    private bool IsValidRegistration(string name, int milliseconds, Closure callback)
    {
        if (string.IsNullOrWhiteSpace(name) || milliseconds <= 0 || callback is null)
        {
            return false;
        }

        return true;
    }
}
