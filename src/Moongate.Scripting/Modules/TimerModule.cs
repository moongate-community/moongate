using Lua;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Interfaces.Internal;
using Moongate.Scripting.Internal;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Scripting.Modules;

/// <summary>Timers for scripts. Every callback runs as a coroutine, so it may call wait().</summary>
/// <remarks>Built in: the engine constructs it, because its dependencies are engine internals. Hosts never register it.</remarks>
[ScriptModule("timer", "Schedules functions on the game loop.")]
internal sealed class TimerModule
{
    private const string AnonymousOwner = "<anonymous>";

    private readonly ITimerService _timers;
    private readonly IScriptScheduler _scheduler;
    private readonly ScriptOwnership _ownership;

    /// <param name="timers">The wheel that fires the callbacks, on the game loop.</param>
    /// <param name="scheduler">Starts each callback as a coroutine and names the current owner.</param>
    /// <param name="ownership">Records every timer under the file that created it.</param>
    public TimerModule(ITimerService timers, IScriptScheduler scheduler, ScriptOwnership ownership)
    {
        _timers = timers;
        _scheduler = scheduler;
        _ownership = ownership;
    }

    /// <summary>Runs <paramref name="fn"/> once, <paramref name="seconds"/> from now, as a coroutine.</summary>
    /// <returns>A handle for <see cref="Cancel"/>.</returns>
    [ScriptFunction(helpText: "Runs fn once after the given seconds. Returns a handle for cancel.")]
    public string After(double seconds, LuaValue fn)
    {
        return Schedule(seconds, fn, repeat: false);
    }

    /// <summary>Runs <paramref name="fn"/> every <paramref name="seconds"/> until cancelled, each run as a coroutine.</summary>
    /// <returns>A handle for <see cref="Cancel"/>.</returns>
    [ScriptFunction(helpText: "Runs fn every given seconds until cancelled. Returns a handle for cancel.")]
    public string Every(double seconds, LuaValue fn)
    {
        return Schedule(seconds, fn, repeat: true);
    }

    /// <summary>Cancels a pending timer by handle.</summary>
    /// <returns>False when no timer with that handle is pending.</returns>
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

        // The wheel refuses a non-positive interval by throwing; refuse it here, before it is touched,
        // so the script sees a Lua argument error instead of the loop seeing a failing work item.
        if (double.IsNaN(seconds) || seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "timer needs a positive number of seconds");
        }

        var function = fn.Read<LuaFunction>();
        var owner = _scheduler.CurrentOwner ?? AnonymousOwner;
        var interval = TimeSpan.FromSeconds(seconds);
        string? handle = null;
        handle = _timers.RegisterTimer(
            "lua-timer:" + owner,
            interval,
            () =>
            {
                if (!repeat && handle is not null)
                {
                    _ownership.ForgetTimer(handle);
                }

                _scheduler.Start(function, owner);
            },
            delay: null,
            repeat: repeat
        );
        _ownership.TrackTimer(owner, handle);

        return handle;
    }
}
