using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>A game loop that only answers IsOnLoopThread and runs posted work inline.</summary>
public sealed class StubGameLoop : IGameLoopService
{
    public bool IsOnLoopThread { get; set; } = true;
    public Task Completion { get; } = new TaskCompletionSource().Task;

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public Task StopAsync(IGameLoopWorkItem finalWorkItem) => Task.CompletedTask;
    public GameLoopMetricsSnapshot GetMetricsSnapshot() => throw new NotSupportedException();

    public bool TryPost(IGameLoopWorkItem workItem)
    {
        workItem.Execute();
        return true;
    }

    public ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default)
    {
        workItem.Execute();
        return ValueTask.CompletedTask;
    }
}
