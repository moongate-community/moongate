using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Core.Interfaces;

/// <summary>
/// The pieces of the SquidStd game loop that gameplay services need: the main-thread dispatcher
/// to marshal work onto the loop, and the timer service to schedule delayed/recurring callbacks.
/// The convenience helpers wrap the common calls so services take one handle instead of both.
/// </summary>
public interface IGameLoopContext
{
    /// <summary>Marshals callbacks onto the main game-loop thread.</summary>
    IMainThreadDispatcher Dispatcher { get; }

    /// <summary>Schedules one-shot and recurring timer callbacks on the loop.</summary>
    ITimerService Timers { get; }

    /// <summary>Runs <paramref name="action" /> on the game-loop thread on the next frame.</summary>
    void Post(Action action);

    /// <summary>
    /// Schedules <paramref name="callback" /> to run once on the loop after <paramref name="delay" />.
    /// </summary>
    /// <returns>The timer id, usable with <see cref="Cancel" />.</returns>
    string Schedule(string name, TimeSpan delay, Action callback);

    /// <summary>
    /// Schedules <paramref name="callback" /> to run on the loop every <paramref name="interval" />,
    /// after an optional initial <paramref name="delay" />.
    /// </summary>
    /// <returns>The timer id, usable with <see cref="Cancel" />.</returns>
    string ScheduleRepeating(string name, TimeSpan interval, Action callback, TimeSpan? delay = null);

    /// <summary>Cancels a scheduled timer by id. Returns true when a timer was removed.</summary>
    bool Cancel(string timerId);
}
