using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>
///     A game loop that only answers IsOnLoopThread and runs posted work inline, counting how many items were posted.
/// </summary>
public sealed class StubGameLoop : IGameLoopService
{
    public bool IsOnLoopThread { get; set; } = true;
    public Task Completion { get; } = new TaskCompletionSource().Task;

    /// <summary>
    ///     Gets the number of work items handed to TryPost or PostAsync.
    /// </summary>
    public int PostedWorkItems { get; private set; }

    /// <summary>
    ///     When true, PostAsync reports IsOnLoopThread as true for the duration of the posted item's Execute, restoring the
    ///     previous value afterwards.
    /// </summary>
    public bool SimulateLoopThreadWhilePosting { get; set; }

    /// <summary>
    ///     Gets or sets whether TryPost refuses work, as a stopping loop does.
    /// </summary>
    public bool RefuseTryPost { get; set; }

    /// <summary>
    ///     Gets or sets whether TryPost keeps the work for <see cref="RunDeferred" /> instead of running it at once, as the
    ///     loop runs a posted item after the current one.
    /// </summary>
    public bool DeferTryPost { get; set; }

    /// <summary>
    ///     Gets the work TryPost kept while <see cref="DeferTryPost" /> is set.
    /// </summary>
    public List<IGameLoopWorkItem> Deferred { get; } = [];

    /// <summary>
    ///     When true, PostAsync refuses the item with the real loop's "not accepting work" error after counting the attempt.
    /// </summary>
    public bool ThrowOnPost { get; set; }

    public GameLoopMetricsSnapshot GetMetricsSnapshot()
    {
        throw new NotSupportedException();
    }

    public ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default)
    {
        PostedWorkItems++;

        if (ThrowOnPost)
        {
            throw new InvalidOperationException("The game loop is not accepting work.");
        }

        if (!SimulateLoopThreadWhilePosting)
        {
            workItem.Execute();

            return ValueTask.CompletedTask;
        }

        var previous = IsOnLoopThread;
        IsOnLoopThread = true;

        try
        {
            workItem.Execute();
        }
        finally
        {
            IsOnLoopThread = previous;
        }

        return ValueTask.CompletedTask;
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(IGameLoopWorkItem finalWorkItem)
    {
        return Task.CompletedTask;
    }

    public Task StopWithFinalWorkAsync(
        Func<Func<IGameLoopWorkItem, Task>, CancellationToken, Task> finalWorkAsync,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public bool TryPost(IGameLoopWorkItem workItem)
    {
        if (RefuseTryPost)
        {
            return false;
        }

        PostedWorkItems++;

        if (DeferTryPost)
        {
            Deferred.Add(workItem);

            return true;
        }

        workItem.Execute();

        return true;
    }

    /// <summary>
    ///     Runs the work TryPost kept, and the work it posts in turn, in order.
    /// </summary>
    public void RunDeferred()
    {
        while (Deferred.Count > 0)
        {
            var next = Deferred[0];
            Deferred.RemoveAt(0);
            next.Execute();
        }
    }
}
