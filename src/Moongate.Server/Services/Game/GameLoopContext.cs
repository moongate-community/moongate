using Moongate.Core.Interfaces;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Server.Services.Game;

/// <summary>
/// Server-side <see cref="IGameLoopContext" />: bundles the SquidStd main-thread dispatcher and
/// timer service so gameplay services take one game-loop handle instead of depending on both.
/// </summary>
public sealed class GameLoopContext : IGameLoopContext
{
    public IMainThreadDispatcher Dispatcher { get; }

    public ITimerService Timers { get; }

    public GameLoopContext(IMainThreadDispatcher dispatcher, ITimerService timers)
    {
        Dispatcher = dispatcher;
        Timers = timers;
    }

    public void Post(Action action)
    {
        Dispatcher.Post(action);
    }

    public string Schedule(string name, TimeSpan delay, Action callback)
    {
        return Timers.RegisterTimer(name, delay, callback, repeat: false);
    }

    public string ScheduleRepeating(string name, TimeSpan interval, Action callback, TimeSpan? delay = null)
    {
        return Timers.RegisterTimer(name, interval, callback, delay, repeat: true);
    }

    public bool Cancel(string timerId)
    {
        return Timers.UnregisterTimer(timerId);
    }
}
