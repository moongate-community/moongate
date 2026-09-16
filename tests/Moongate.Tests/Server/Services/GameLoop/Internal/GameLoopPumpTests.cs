using System.Threading.Channels;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Services.GameLoop.Internal;
using Moongate.Tests.Support.GameLoop;

namespace Moongate.Tests.Server.Services.GameLoop.Internal;

public sealed class GameLoopPumpTests
{
    [Fact]
    public void RunBatch_BudgetBoundary_DoesNotDequeueOrDropNextItem()
    {
        var channel = Channel.CreateUnbounded<IGameLoopWorkItem>();
        var executed = new List<int>();
        channel.Writer.TryWrite(new ActionGameLoopWorkItem(() => executed.Add(1)));
        channel.Writer.TryWrite(new ActionGameLoopWorkItem(() => executed.Add(2)));
        channel.Writer.TryWrite(new ActionGameLoopWorkItem(() => executed.Add(3)));
        var pump = new GameLoopPump(channel.Reader, 2);

        Assert.Equal(2, pump.RunBatch());
        Assert.Equal(new[] { 1, 2 }, executed);
        Assert.Equal(1, pump.RunBatch());
        Assert.Equal(new[] { 1, 2, 3 }, executed);
        Assert.Equal(0, pump.RunBatch());
    }

    [Fact]
    public void RunBatch_HandlerFailure_PropagatesOriginalExceptionWithoutDequeueingNextItem()
    {
        var channel = Channel.CreateUnbounded<IGameLoopWorkItem>();
        var failure = new ApplicationException("fatal handler");
        var next = new ActionGameLoopWorkItem(() => { });
        channel.Writer.TryWrite(new ActionGameLoopWorkItem(() => throw failure));
        channel.Writer.TryWrite(next);
        var pump = new GameLoopPump(channel.Reader, 2);

        Assert.Same(failure, Assert.Throws<ApplicationException>(() => pump.RunBatch()));
        Assert.True(channel.Reader.TryRead(out var remaining));
        Assert.Same(next, remaining);
    }
}
