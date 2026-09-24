using System.Threading.Channels;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Data.GameLoop.Internal;
using Moongate.Server.Services.GameLoop.Internal;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Timing;

namespace Moongate.Tests.Server.Services.GameLoop.Internal;

public sealed class GameLoopPumpTests
{
    [Fact]
    public void RunBatch_BudgetBoundary_DoesNotDequeueOrDropNextItem()
    {
        var channel = Channel.CreateUnbounded<QueuedGameLoopWorkItem>();
        var executed = new List<int>();
        Write(channel, new ActionGameLoopWorkItem(() => executed.Add(1)));
        Write(channel, new ActionGameLoopWorkItem(() => executed.Add(2)));
        Write(channel, new ActionGameLoopWorkItem(() => executed.Add(3)));
        var pump = new GameLoopPump(channel.Reader, 2, new ManualTimeProvider(), TimeSpan.FromMilliseconds(5));

        Assert.Equal(2, pump.RunBatch());
        Assert.Equal(new[] { 1, 2 }, executed);
        Assert.Equal(1, pump.RunBatch());
        Assert.Equal(new[] { 1, 2, 3 }, executed);
        Assert.Equal(0, pump.RunBatch());
    }

    [Fact]
    public void RunBatch_HandlerFailure_PropagatesOriginalExceptionWithoutDequeueingNextItem()
    {
        var channel = Channel.CreateUnbounded<QueuedGameLoopWorkItem>();
        var failure = new ApplicationException("fatal handler");
        var next = new ActionGameLoopWorkItem(() => { });
        Write(channel, new ActionGameLoopWorkItem(() => throw failure));
        Write(channel, next);
        var pump = new GameLoopPump(channel.Reader, 2, new ManualTimeProvider(), TimeSpan.FromMilliseconds(5));

        Assert.Same(failure, Assert.Throws<ApplicationException>(() => pump.RunBatch()));
        Assert.True(channel.Reader.TryRead(out var remaining));
        Assert.Same(next, remaining.WorkItem);
    }

    [Fact]
    public void RunBatch_TimeBudgetBoundary_RetainsNextItemForNextBatch()
    {
        var clock = new ManualTimeProvider(1000);
        var channel = Channel.CreateUnbounded<QueuedGameLoopWorkItem>();
        var executed = new List<int>();
        Write(
            channel,
            new ActionGameLoopWorkItem(
                () =>
                {
                    executed.Add(1);
                    clock.Advance(TimeSpan.FromMilliseconds(5));
                }
            )
        );
        Write(channel, new ActionGameLoopWorkItem(() => executed.Add(2)));
        var pump = new GameLoopPump(channel.Reader, 10, clock, TimeSpan.FromMilliseconds(5));

        Assert.Equal(1, pump.RunBatch());
        Assert.Equal(new[] { 1 }, executed);
        Assert.Equal(1, pump.RunBatch());
        Assert.Equal(new[] { 1, 2 }, executed);
    }

    private static void Write(Channel<QueuedGameLoopWorkItem> channel, IGameLoopWorkItem workItem)
        => channel.Writer.TryWrite(new(workItem, 0));
}
