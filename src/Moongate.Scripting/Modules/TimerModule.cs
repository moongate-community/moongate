using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Scripting.Modules;

/// <summary>Timers for scripts. Every callback runs as a coroutine, so it may call wait().</summary>
[ScriptModule("timer", "Schedules functions on the game loop.")]
internal sealed class TimerModule
{
    private const string AnonymousOwner = "<anonymous>";

    private readonly ITimerService _timers;
    private readonly IScriptScheduler _scheduler;
    private readonly ScriptOwnership _ownership;

    public TimerModule(ITimerService timers, IScriptScheduler scheduler, ScriptOwnership ownership)
    {
        _timers = timers;
        _scheduler = scheduler;
        _ownership = ownership;
    }

    [ScriptFunction(helpText: "Runs fn once after the given seconds. Returns a handle for cancel.")]
    public string After(double seconds, LuaValue fn)
    {
        return Schedule(seconds, fn, repeat: false);
    }

    [ScriptFunction(helpText: "Runs fn every given seconds until cancelled. Returns a handle for cancel.")]
    public string Every(double seconds, LuaValue fn)
    {
        return Schedule(seconds, fn, repeat: true);
    }

    [ScriptFunction(helpText: "Cancels a timer by handle. Returns false when no such timer is pending.")]
    public bool Cancel(string handle)
    {
        _ownership.ForgetTimer(handle);

        return _timers.UnregisterTimer(handle);
    }

    private string Schedule(double seconds, LuaValue fn, bool repeat)
    {
        if (fn.Type != LuaValueType.Function)
        {
            throw new ArgumentException($"expected a function, got {fn.TypeToString()}", nameof(fn));
        }

        if (double.IsNaN(seconds) || seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "seconds must be a non-negative number");
        }

        var function = fn.Read<LuaFunction>();
        var owner = _scheduler.CurrentOwner ?? AnonymousOwner;
        var interval = TimeSpan.FromSeconds(seconds);
        string? handle = null;
        handle = _timers.RegisterTimer("lua-timer:" + owner, interval, () =>
        {
            if (!repeat && handle is not null)
            {
                _ownership.ForgetTimer(handle);
            }

            _scheduler.Start(function, owner);
        }, delay: null, repeat: repeat);
        _ownership.TrackTimer(owner, handle);

        return handle;
    }
}
