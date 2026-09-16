namespace Moongate.Server.Core.Data.GameLoop;

/// <summary>Bounds inbox memory, attempted handlers and cooperative elapsed time per command batch.</summary>
public sealed class GameLoopOptions
{
    private int _queueCapacity = 4096;
    private int _maxWorkItemsPerBatch = 256;

    private TimeSpan _workItemBudget = TimeSpan.FromMilliseconds(5);

    /// <summary>Cooperative duration limit per command batch; an active handler is never interrupted.</summary>
    public TimeSpan WorkItemBudget
    {
        get => _workItemBudget;
        init
        {
            if (value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
            _workItemBudget = value;
        }
    }

    /// <summary>Maximum queued items, excluding the currently executing handler. Must be positive.</summary>
    public int QueueCapacity
    {
        get => _queueCapacity;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _queueCapacity = value;
        }
    }

    /// <summary>Maximum attempted handlers per pump batch. Must be positive.</summary>
    public int MaxWorkItemsPerBatch
    {
        get => _maxWorkItemsPerBatch;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxWorkItemsPerBatch = value;
        }
    }
}
