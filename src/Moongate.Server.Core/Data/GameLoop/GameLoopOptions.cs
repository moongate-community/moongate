namespace Moongate.Server.Core.Data.GameLoop;

/// <summary>Bounds inbox memory and the number of attempted handlers in one pump batch.</summary>
public sealed class GameLoopOptions
{
    private int _queueCapacity = 4096;
    private int _maxWorkItemsPerBatch = 256;

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
