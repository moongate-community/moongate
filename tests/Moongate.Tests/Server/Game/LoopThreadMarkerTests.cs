using Moongate.Server.Services.Game;

namespace Moongate.Tests.Server.Game;

public class LoopThreadMarkerTests
{
    [Fact]
    public void IsOnLoopThread_FalseBeforeCapture()
    {
        var marker = new LoopThreadMarker();

        Assert.False(marker.IsOnLoopThread);
    }

    [Fact]
    public void IsOnLoopThread_TrueOnCapturingThread_FalseElsewhere()
    {
        var marker = new LoopThreadMarker();
        marker.Capture();

        Assert.True(marker.IsOnLoopThread);

        // A dedicated Thread always has a distinct ManagedThreadId from the still-alive test thread
        // (unlike a ThreadPool thread, whose id can be reused under load).
        var offThreadResult = true;
        var thread = new Thread(() => offThreadResult = marker.IsOnLoopThread);
        thread.Start();
        thread.Join();

        Assert.False(offThreadResult);
    }
}
