namespace Moongate.Tests.Support;

/// <summary>
/// A <see cref="TimeProvider" /> whose current instant can be set or advanced, for tests that need to
/// move time forward deterministically. <see cref="TimeProvider.GetLocalNow" /> returns the same instant.
/// </summary>
public sealed class MutableTimeProvider : TimeProvider
{
    private readonly object _sync = new();
    private readonly List<MutableTimer> _timers = [];

    private DateTimeOffset _now;
    private long _timestamp;

    public MutableTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    /// <summary>
    /// A clock starting at the real instant, which is what any test involving a JWT needs.
    /// <para>
    /// The API issues tokens from the injected provider but validates their lifetime against the system
    /// clock, because <c>TokenValidationParameters</c> offers no way to supply one. In production both are
    /// the system clock and the two agree; in a test they only agree if this clock starts near the real
    /// instant. A hardcoded date makes the suite pass or fail depending on the hour it is run — which is
    /// exactly what it did before this existed.
    /// </para>
    /// </summary>
    public static MutableTimeProvider StartingNow()
        => new(DateTimeOffset.UtcNow);

    public DateTimeOffset Now
    {
        get
        {
            lock (_sync)
            {
                return _now;
            }
        }
        set
        {
            lock (_sync)
            {
                SetNow(value);
            }
        }
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
        => Now;

    public override long GetTimestamp()
    {
        lock (_sync)
        {
            return _timestamp;
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        ArgumentNullException.ThrowIfNull(callback);

        var timer = new MutableTimer(this, callback, state);

        lock (_sync)
        {
            _timers.Add(timer);
            ChangeTimer(timer, dueTime, period);
        }

        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        var target = Now + delta;

        while (true)
        {
            MutableTimer? timer;

            lock (_sync)
            {
                timer = _timers
                    .Where(candidate => candidate.NextDue is { } due && due <= target)
                    .MinBy(candidate => candidate.NextDue);

                if (timer is null)
                {
                    SetNow(target);
                    return;
                }

                SetNow(timer.NextDue!.Value);
                timer.PrepareCallback();
            }

            timer.InvokeCallback();
        }
    }

    private void SetNow(DateTimeOffset value)
    {
        _timestamp += (value - _now).Ticks;
        _now = value;
    }

    private static void ValidateTimerDuration(TimeSpan value, string parameterName)
    {
        if (value < Timeout.InfiniteTimeSpan || value.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private bool ChangeTimer(MutableTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        ValidateTimerDuration(dueTime, nameof(dueTime));
        ValidateTimerDuration(period, nameof(period));

        lock (_sync)
        {
            if (timer.IsDisposed)
            {
                return false;
            }

            timer.Period = period;
            timer.NextDue = dueTime == Timeout.InfiniteTimeSpan ? null : _now + dueTime;

            return true;
        }
    }

    private void DisposeTimer(MutableTimer timer)
    {
        lock (_sync)
        {
            timer.IsDisposed = true;
            timer.NextDue = null;
            _timers.Remove(timer);
        }
    }

    private sealed class MutableTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly MutableTimeProvider _owner;
        private readonly object? _state;

        public bool IsDisposed { get; set; }

        public DateTimeOffset? NextDue { get; set; }

        public TimeSpan Period { get; set; }

        public MutableTimer(MutableTimeProvider owner, TimerCallback callback, object? state)
        {
            _callback = callback;
            _owner = owner;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
            => _owner.ChangeTimer(this, dueTime, period);

        public void InvokeCallback()
            => _callback(_state);

        public void PrepareCallback()
        {
            if (Period > TimeSpan.Zero)
            {
                NextDue += Period;
                return;
            }

            NextDue = null;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            _owner.DisposeTimer(this);
        }
    }
}
