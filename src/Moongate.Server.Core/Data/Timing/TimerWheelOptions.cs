namespace Moongate.Server.Core.Data.Timing;

/// <summary>
///     Bounds timer retention, wheel precision and cooperative callback batches.
/// </summary>
public sealed class TimerWheelOptions
{
    private readonly TimeSpan _tickDuration = TimeSpan.FromMilliseconds(8);
    private readonly int _wheelSize = 512;
    private readonly int _maxPendingTimers = 65536;
    private readonly int _maxCallbacksPerBatch = 256;
    private readonly TimeSpan _callbackBudget = TimeSpan.FromMilliseconds(5);

    /// <summary>
    ///     Wheel resolution; deadlines round upward to this positive duration.
    /// </summary>
    public TimeSpan TickDuration
    {
        get => _tickDuration;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _tickDuration = value;
        }
    }

    /// <summary>
    ///     Number of buckets visited at most once when catching up after a pause.
    /// </summary>
    public int WheelSize
    {
        get => _wheelSize;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _wheelSize = value;
        }
    }

    /// <summary>
    ///     Maximum registrations, including ready and repeating in-flight callbacks.
    /// </summary>
    public int MaxPendingTimers
    {
        get => _maxPendingTimers;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxPendingTimers = value;
        }
    }

    /// <summary>
    ///     Maximum attempted callbacks per batch.
    /// </summary>
    public int MaxCallbacksPerBatch
    {
        get => _maxCallbacksPerBatch;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxCallbacksPerBatch = value;
        }
    }

    /// <summary>
    ///     Cooperative elapsed-time budget; one due callback is always allowed.
    /// </summary>
    public TimeSpan CallbackBudget
    {
        get => _callbackBudget;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _callbackBudget = value;
        }
    }
}
