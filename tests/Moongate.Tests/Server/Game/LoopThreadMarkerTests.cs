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

        var offThread = Task.Run(() => marker.IsOnLoopThread);

        Assert.False(offThread.Result);
    }
}
