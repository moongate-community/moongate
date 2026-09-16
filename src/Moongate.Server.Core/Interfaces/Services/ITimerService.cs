using Moongate.Server.Core.Data.Timing;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Registers synchronous callbacks for execution on the dedicated game loop thread.</summary>
public interface ITimerService : IMoongateStartupService
{
    /// <summary>Registers a timer with a positive interval and optional positive first delay. Duplicate names are allowed.</summary>
    /// <remarks>
    /// Deadlines round upward to the wheel resolution. Repeats use fixed-rate deadlines and coalesce missed occurrences.
    /// Callbacks execute synchronously on the game loop and must not block on work dispatched to that same loop.
    /// Registration is allowed before startup and rejected after shutdown or when registration capacity is exhausted.
    /// </remarks>
    string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false);

    /// <summary>Cancels a registration before its next callback is claimed; a running callback may finish.</summary>
    bool UnregisterTimer(string timerId);

    /// <summary>Cancels every registration with the given logical name and returns the number removed.</summary>
    int UnregisterTimersByName(string name);

    /// <summary>Cancels every registration, including repeating callbacks currently executing.</summary>
    void UnregisterAllTimers();

    /// <summary>Returns a coherent snapshot of scheduling and callback measurements.</summary>
    TimerMetricsSnapshot GetMetricsSnapshot();
}
