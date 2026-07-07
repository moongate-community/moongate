using Moongate.Core.Interfaces;
using MoonSharp.Interpreter;
using Serilog;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Scripting.Modules;

/// <summary>
/// Exposes the game loop to Lua: run work on the loop thread and schedule one-shot / recurring
/// timers. All callbacks run on the game-loop thread, so scripts see consistent game state.
/// </summary>
[ScriptModule("game", "Run work on the game-loop thread and schedule timers.")]
public sealed class GameLoopModule
{
    private readonly ILogger _logger = Log.Logger.ForContext<GameLoopModule>();
    private readonly IGameLoopContext _context;

    public GameLoopModule(IGameLoopContext context)
    {
        _context = context;
    }

    [ScriptFunction("post", "Runs the callback on the game-loop thread on the next frame.")]
    public void Post(Closure callback)
    {
        _context.Post(() => Invoke(callback));
    }

    [ScriptFunction("schedule", "Runs the callback once after delayMs; returns the timer id.")]
    public string Schedule(string name, double delayMs, Closure callback)
    {
        return _context.Schedule(name, TimeSpan.FromMilliseconds(delayMs), () => Invoke(callback));
    }

    [ScriptFunction("schedule_repeating", "Runs the callback every intervalMs; returns the timer id.")]
    public string ScheduleRepeating(string name, double intervalMs, Closure callback)
    {
        return _context.ScheduleRepeating(name, TimeSpan.FromMilliseconds(intervalMs), () => Invoke(callback));
    }

    [ScriptFunction("cancel", "Cancels a scheduled timer by its id. Returns true when removed.")]
    public bool Cancel(string timerId)
    {
        return _context.Cancel(timerId);
    }

    private void Invoke(Closure callback)
    {
        try
        {
            callback.Call();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lua game-loop callback threw");
        }
    }
}
