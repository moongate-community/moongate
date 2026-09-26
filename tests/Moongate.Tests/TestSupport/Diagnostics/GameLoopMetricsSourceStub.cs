using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class GameLoopMetricsSourceStub : IGameLoopService
{
    public GameLoopMetricsSnapshot Snapshot { get; set; }
    public int SnapshotReadCount { get; private set; }
    public bool IsOnLoopThread => throw new NotSupportedException();
    public Task Completion => throw new NotSupportedException();

    public GameLoopMetricsSourceStub(GameLoopMetricsSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public GameLoopMetricsSnapshot GetMetricsSnapshot()
    {
        SnapshotReadCount++;

        return Snapshot;
    }

    public ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task StartAsync()
    {
        throw new NotSupportedException();
    }

    public Task StopAsync()
    {
        throw new NotSupportedException();
    }

    public Task StopAsync(IGameLoopWorkItem finalWorkItem)
    {
        throw new NotSupportedException();
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
        throw new NotSupportedException();
    }
}
