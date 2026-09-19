using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>A game loop that only answers IsOnLoopThread and runs posted work inline, counting how many items were posted.</summary>
public sealed class StubGameLoop : IGameLoopService
{
    public bool IsOnLoopThread { get; set; } = true;
    public Task Completion { get; } = new TaskCompletionSource().Task;

    /// <summary>Gets the number of work items handed to TryPost or PostAsync.</summary>
    public int PostedWorkItems { get; private set; }

    /// <summary>When true, PostAsync reports IsOnLoopThread as true for the duration of the posted item's Execute, restoring the previous value afterwards.</summary>
    public bool SimulateLoopThreadWhilePosting { get; set; }

    /// <summary>When true, PostAsync refuses the item with the real loop's "not accepting work" error after counting the attempt.</summary>
    public bool ThrowOnPost { get; set; }

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public Task StopAsync(IGameLoopWorkItem finalWorkItem) => Task.CompletedTask;
    public GameLoopMetricsSnapshot GetMetricsSnapshot() => throw new NotSupportedException();

    public bool TryPost(IGameLoopWorkItem workItem)
    {
        PostedWorkItems++;
        workItem.Execute();
        return true;
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
}
