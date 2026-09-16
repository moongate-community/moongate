using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Data.GameLoop.Internal;

internal readonly record struct QueuedGameLoopWorkItem
{
    public IGameLoopWorkItem WorkItem { get; }
    public long EnqueuedAt { get; }

    public QueuedGameLoopWorkItem(IGameLoopWorkItem workItem, long enqueuedAt)
    {
        WorkItem = workItem;
        EnqueuedAt = enqueuedAt;
    }
}
