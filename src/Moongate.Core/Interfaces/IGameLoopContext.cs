using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Core.Interfaces;

/// <summary>
/// The pieces of the SquidStd game loop that gameplay services need: the main-thread dispatcher
/// to marshal work onto the loop, and the timer service to schedule delayed/recurring callbacks.
/// </summary>
public interface IGameLoopContext
{
    /// <summary>Marshals callbacks onto the main game-loop thread.</summary>
    IMainThreadDispatcher Dispatcher { get; }

    /// <summary>Schedules one-shot and recurring timer callbacks on the loop.</summary>
    ITimerService Timers { get; }
}
