using Moongate.Server.Services.Game;
using Moongate.Tests.Support;
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
