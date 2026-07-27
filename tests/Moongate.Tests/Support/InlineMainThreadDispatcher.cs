using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Tests.Support;

/// <summary>Runs posted callbacks synchronously on the calling thread — a game loop that drains instantly.</summary>
public sealed class InlineMainThreadDispatcher : IMainThreadDispatcher
{
    public int PendingCount => 0;

    public int DrainPending(double? budgetMs = null)
        => 0;

    public void Post(Action action)
        => action();
}
