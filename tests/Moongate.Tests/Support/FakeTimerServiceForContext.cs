using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Tests.Support;

/// <summary>
/// Stands in for the real timer wheel: <see cref="Moongate.Server.Services.Game.GameLoopContext" /> only
/// needs an <see cref="ITimerService" /> to satisfy its constructor for
/// <c>GameLoopContext.InvokeAsync{T}</c> tests, which never touch timers.
/// </summary>
public sealed class FakeTimerServiceForContext : ITimerService
{
    public string RegisterTimer(
        string name,
        TimeSpan interval,
        Action callback,
        TimeSpan? delay = null,
        bool repeat = false
    )
        => name;

    public void UnregisterAllTimers()
    {
    }

    public bool UnregisterTimer(string timerId)
        => true;

    public int UnregisterTimersByName(string name)
        => 0;

    public int UpdateTicksDelta(long timestampMilliseconds)
        => 0;
}
