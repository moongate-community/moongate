using Moongate.Server.Services.Game;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Game;

public class GameLoopContextTests
{
    [Fact]
    public async Task InvokeAsync_RunsWorkOnLoop_ReturnsResult()
    {
        var dispatcher = new MainThreadDispatcherService();
        var timers = new FakeTimerServiceForContext();
        var loop = new GameLoopContext(dispatcher, timers);

        var task = loop.InvokeAsync(() => 21 * 2);
        dispatcher.DrainPending();

        Assert.Equal(42, await task);
    }

    [Fact]
    public async Task InvokeAsync_LoopNeverRuns_TimesOut()
    {
        var dispatcher = new MainThreadDispatcherService();
        var timers = new FakeTimerServiceForContext();
        var loop = new GameLoopContext(dispatcher, timers);

        var task = loop.InvokeAsync(() => 1, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(async () => await task);
    }
}

/// <summary>
/// Stands in for the real timer wheel: <see cref="GameLoopContext" /> only needs an
/// <see cref="SquidStd.Core.Interfaces.Timing.ITimerService" /> to satisfy its constructor for these
/// tests, which exercise <see cref="GameLoopContext.InvokeAsync{T}" /> and never touch timers.
/// </summary>
internal sealed class FakeTimerServiceForContext : SquidStd.Core.Interfaces.Timing.ITimerService
{
    public string RegisterTimer(
        string name,
        System.TimeSpan interval,
        System.Action callback,
        System.TimeSpan? delay = null,
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
