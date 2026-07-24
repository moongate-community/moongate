using Moongate.Server.Services.Game;
using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Tests.Server.Game;

public class EventLoopThreadTests
{
    private sealed class FakeEventLoop : IEventLoopService
    {
        public long TickCount => 0;
        public double AverageTickMs => 0;
        public double MaxTickMs => 0;
        public bool IsOnLoopThread { get; set; }
    }

    [Fact]
    public void IsOnLoopThread_MirrorsTheEventLoop()
    {
        var loop = new FakeEventLoop { IsOnLoopThread = true };
        var thread = new EventLoopThread(loop);

        Assert.True(thread.IsOnLoopThread);

        loop.IsOnLoopThread = false;

        Assert.False(thread.IsOnLoopThread);
    }
}
