using Moongate.Server.Services.GameLoop.Internal;
using Moongate.Tests.Support.GameLoop;

namespace Moongate.Tests.Server.Services.GameLoop.Internal;

public sealed class GameLoopFinalWorkSessionTests
{
    [Fact]
    public async Task CloseAsync_PendingCapture_DrainsItAndRejectsEarlyReturn()
    {
        var session = new GameLoopFinalWorkSession(() => { }, CancellationToken.None);
        var ran = false;
        var capture = session.DispatchAsync(new ActionGameLoopWorkItem(() => ran = true));
        var closing = session.CloseAsync();
        Assert.False(closing.IsCompleted);
        Assert.False(ran);
        session.ExecutePending();
        await Assert.ThrowsAsync<InvalidOperationException>(() => closing);
        await capture;
        Assert.True(ran);
        Assert.False(session.ExecutePending());
    }

    [Fact]
    public async Task CloseAsync_AlreadyFaultedCapture_DoesNotHideFailure()
    {
        var session = new GameLoopFinalWorkSession(() => { }, CancellationToken.None);
        var failure = new IOException("capture failed");
        var capture = session.DispatchAsync(new ActionGameLoopWorkItem(() => throw failure));
        session.ExecutePending();
        Assert.Same(failure, await Record.ExceptionAsync(() => capture));
        Assert.Same(failure, await Record.ExceptionAsync(session.CloseAsync));
    }
}
