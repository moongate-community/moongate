using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Timing.Internal;

namespace Moongate.Server.Services.Timing;

/// <summary>A bounded hashed wheel whose callbacks are driven only by its bound game loop thread.</summary>
public sealed class TimerWheelService : ITimerService
{
    private readonly Lock _syncRoot = new();
    private readonly TimerWheelOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly long _originTimestamp;
    private readonly Dictionary<string, HashSet<string>> _timerIdsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimerEntry> _timersById = new(StringComparer.Ordinal);
    private readonly LinkedList<TimerEntry>[] _wheel;

    private readonly SortedSet<TimerEntry> _ready = new(
        Comparer<TimerEntry>.Create(
            (left, right) =>
            {
                var due = left.DueTick.CompareTo(right.DueTick);

                return due != 0 ? due : left.Sequence.CompareTo(right.Sequence);
            }
        )
    );

    private Action? _wakeUp;
    private int? _ownerThreadId;
    private bool _processing;
    private bool _closed;
    private long _processedTick;
    private long _registeredTimers;
    private long _executedCallbacks;
    private long _callbackFaults;
    private long _coalescedOccurrences;
    private TimeSpan _maxLateness;
    private TimeSpan _maxCallbackDuration;
    private TimeSpan _lastBatchDuration;

    public TimerWheelService(TimerWheelOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _originTimestamp = timeProvider.GetTimestamp();
        _wheel = new LinkedList<TimerEntry>[options.WheelSize];

        for (var i = 0; i < _wheel.Length; i++)
        {
            _wheel[i] = new();
        }
    }

    public TimerMetricsSnapshot GetMetricsSnapshot()
    {
        lock (_syncRoot)
        {
            return new()
            {
                ActiveTimers = _timersById.Count,
                RegisteredTimers = _registeredTimers,
                ExecutedCallbacks = _executedCallbacks,
                CallbackFaults = _callbackFaults,
                CoalescedOccurrences = _coalescedOccurrences,
                MaxLateness = _maxLateness,
                MaxCallbackDuration = _maxCallbackDuration,
                LastBatchDuration = _lastBatchDuration
            };
        }
    }

    public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(callback);
        var firstDelay = delay ?? interval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(firstDelay, TimeSpan.Zero, nameof(delay));
        TimerEntry entry;
        Action? wakeUp;

        lock (_syncRoot)
        {
            ThrowIfClosed();

            if (_timersById.Count >= _options.MaxPendingTimers)
            {
                throw new InvalidOperationException("Timer capacity has been reached.");
            }

            // Round the registration instant upward before the wheel deadline, never shortening its delay.
            var nominalDeadline = checked(GetElapsedTicks(true) + firstDelay.Ticks);
            var dueTick = ToDueTick(nominalDeadline);
            var sequence = checked(_registeredTimers + 1);
            entry = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Callback = callback,
                IntervalTicks = interval.Ticks,
                Repeat = repeat,
                Sequence = sequence,
                NominalDeadlineTicks = nominalDeadline,
                DueTick = dueTick
            };
            _timersById.Add(entry.Id, entry);

            if (!_timerIdsByName.TryGetValue(name, out var ids))
            {
                ids = new(StringComparer.Ordinal);
                _timerIdsByName.Add(name, ids);
            }

            ids.Add(entry.Id);
            AddToWheel(entry);
            _registeredTimers = sequence;
            wakeUp = _wakeUp;
        }

        wakeUp?.Invoke();

        return entry.Id;
    }

    public Task StartAsync()
    {
        lock (_syncRoot)
        {
            ThrowIfClosed();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Close();

        return Task.CompletedTask;
    }

    public void UnregisterAllTimers()
    {
        Action? wakeUp;

        lock (_syncRoot)
        {
            ClearTimers();
            wakeUp = _wakeUp;
        }

        wakeUp?.Invoke();
    }

    public bool UnregisterTimer(string timerId)
    {
        if (string.IsNullOrWhiteSpace(timerId))
        {
            return false;
        }

        bool removed;
        Action? wakeUp;

        lock (_syncRoot)
        {
            removed = RemoveEntryById(timerId);
            wakeUp = removed ? _wakeUp : null;
        }

        wakeUp?.Invoke();

        return removed;
    }

    public int UnregisterTimersByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        int removed;
        Action? wakeUp;

        lock (_syncRoot)
        {
            if (!_timerIdsByName.TryGetValue(name, out var ids))
            {
                return 0;
            }

            var timerIds = ids.ToArray();

            foreach (var timerId in timerIds)
            {
                RemoveEntryById(timerId);
            }

            removed = timerIds.Length;
            wakeUp = _wakeUp;
        }

        wakeUp?.Invoke();

        return removed;
    }

    internal void BindToCurrentThread(Action wakeUp)
    {
        lock (_syncRoot)
        {
            ThrowIfClosed();

            if (_ownerThreadId.HasValue)
            {
                throw new InvalidOperationException("The timer driver is already bound.");
            }

            _ownerThreadId = Environment.CurrentManagedThreadId;
            _wakeUp = wakeUp;
        }
    }

    internal void Close()
    {
        lock (_syncRoot)
        {
            CloseCore();
        }
    }

    internal TimeSpan? GetNextDelay()
    {
        lock (_syncRoot)
        {
            if (_ready.Count > 0)
            {
                return TimeSpan.Zero;
            }

            long? next = null;

            foreach (var entry in _timersById.Values)
            {
                // A repeating callback being executed is indexed but has no scheduled deadline yet.
                if (entry.Node is not null && (!next.HasValue || entry.DueTick < next.Value))
                {
                    next = entry.DueTick;
                }
            }

            if (!next.HasValue)
            {
                return null;
            }

            var remaining = checked(next.Value * _options.TickDuration.Ticks) - GetElapsedTicks(false);

            return TimeSpan.FromTicks(Math.Max(0, remaining));
        }
    }

    internal int ProcessDueTimers()
    {
        lock (_syncRoot)
        {
            if (_ownerThreadId != Environment.CurrentManagedThreadId || _processing)
            {
                throw new InvalidOperationException("Only the bound thread may drive timers, without reentrancy.");
            }

            if (_closed)
            {
                return 0;
            }

            _processing = true;
        }

        var startedAt = _timeProvider.GetTimestamp();
        var attempted = 0;
        var callbackFaulted = false;

        try
        {
            lock (_syncRoot)
            {
                CollectDueEntries();
            }

            while (attempted < _options.MaxCallbacksPerBatch)
            {
                // Never remove the next ready registration until its callback fits this batch.
                if (attempted > 0 && _timeProvider.GetElapsedTime(startedAt) >= _options.CallbackBudget)
                {
                    break;
                }

                TimerEntry entry;
                long callbackStartedAt;

                lock (_syncRoot)
                {
                    if (_closed || _ready.Count == 0)
                    {
                        break;
                    }

                    entry = _ready.Min!;
                    _ready.Remove(entry);
                    entry.Ready = false;

                    if (_executedCallbacks < long.MaxValue)
                    {
                        _executedCallbacks++;
                    }

                    if (!entry.Repeat)
                    {
                        RemoveFromIndexes(entry);
                    }

                    callbackStartedAt = _timeProvider.GetTimestamp();
                    var lateness = _timeProvider.GetElapsedTime(_originTimestamp, callbackStartedAt) -
                                   TimeSpan.FromTicks(entry.DueTick * _options.TickDuration.Ticks);

                    if (lateness > _maxLateness)
                    {
                        _maxLateness = lateness;
                    }
                }

                attempted++;

                try
                {
                    entry.Callback();
                }
                catch
                {
                    callbackFaulted = true;

                    lock (_syncRoot)
                    {
                        if (_callbackFaults < long.MaxValue)
                        {
                            _callbackFaults++;
                        }

                        CloseCore();
                    }

                    throw;
                }
                finally
                {
                    var elapsed = GetDiagnosticElapsedTime(callbackStartedAt, callbackFaulted);

                    lock (_syncRoot)
                    {
                        if (elapsed > _maxCallbackDuration)
                        {
                            _maxCallbackDuration = elapsed;
                        }
                    }
                }

                lock (_syncRoot)
                {
                    if (entry.Repeat && _timersById.ContainsKey(entry.Id))
                    {
                        RescheduleRepeat(entry);
                    }
                }
            }

            return attempted;
        }
        catch
        {
            // Arithmetic or clock failures also leave no partially scheduled registry behind.
            Close();

            throw;
        }
        finally
        {
            lock (_syncRoot)
            {
                _lastBatchDuration = GetDiagnosticElapsedTime(startedAt, callbackFaulted);
                _processing = false;
            }
        }
    }

    private void AddToWheel(TimerEntry entry)
        => entry.Node = _wheel[(int)(entry.DueTick % _wheel.Length)].AddLast(entry);

    private void ClearTimers()
    {
        // Clear node references as well: an in-flight callback may retain its entry until it returns.
        foreach (var entry in _timersById.Values)
        {
            entry.Node = null;
            entry.Ready = false;
        }

        _timersById.Clear();
        _timerIdsByName.Clear();
        _ready.Clear();

        foreach (var bucket in _wheel)
        {
            bucket.Clear();
        }
    }

    private void CloseCore()
    {
        _closed = true;
        ClearTimers();
        _wakeUp = null;
    }

    private void CollectDueEntries()
    {
        var nowTick = GetElapsedTicks(false) / _options.TickDuration.Ticks;
        var ticksToScan = Math.Min(nowTick - _processedTick, _wheel.Length);

        // A long pause visits at most one revolution, checking absolute deadlines instead of old rounds.
        for (long offset = 1; offset <= ticksToScan; offset++)
        {
            var bucket = _wheel[(int)((_processedTick + offset) % _wheel.Length)];
            var node = bucket.First;

            while (node is not null)
            {
                var next = node.Next;
                var entry = node.Value;

                if (entry.DueTick <= nowTick)
                {
                    bucket.Remove(node);
                    entry.Node = null;
                    entry.Ready = true;
                    _ready.Add(entry);
                }

                node = next;
            }
        }

        _processedTick = nowTick;
    }

    private TimeSpan GetDiagnosticElapsedTime(long startedAt, bool callbackFaulted)
    {
        try
        {
            return _timeProvider.GetElapsedTime(startedAt);
        }
        catch when (callbackFaulted)
        {
            // A secondary diagnostic failure must not replace the original callback exception.
            return TimeSpan.Zero;
        }
    }

    private long GetElapsedTicks(bool roundUp)
    {
        // TimestampFrequency need not equal TimeSpan.TicksPerSecond. Int128 avoids conversion overflow.
        var numerator = ((Int128)_timeProvider.GetTimestamp() - _originTimestamp) * TimeSpan.TicksPerSecond;
        var frequency = _timeProvider.TimestampFrequency;

        return checked((long)((numerator + (roundUp ? frequency - 1 : 0)) / frequency));
    }

    private bool RemoveEntryById(string timerId)
    {
        if (!_timersById.TryGetValue(timerId, out var entry))
        {
            return false;
        }

        if (entry.Node is not null)
        {
            entry.Node.List!.Remove(entry.Node);
            entry.Node = null;
        }

        if (entry.Ready)
        {
            _ready.Remove(entry);
            entry.Ready = false;
        }

        RemoveFromIndexes(entry);

        return true;
    }

    private void RemoveFromIndexes(TimerEntry entry)
    {
        _timersById.Remove(entry.Id);
        var ids = _timerIdsByName[entry.Name];
        ids.Remove(entry.Id);

        if (ids.Count == 0)
        {
            _timerIdsByName.Remove(entry.Name);
        }
    }

    private void RescheduleRepeat(TimerEntry entry)
    {
        // Integer deadlines beyond floor(now) are still in the future, even between TimeSpan ticks.
        var now = GetElapsedTicks(false);
        var skipped = (now - entry.NominalDeadlineTicks) / entry.IntervalTicks;
        var nextNominal = checked((long)((Int128)entry.NominalDeadlineTicks + ((Int128)skipped + 1) * entry.IntervalTicks));
        var dueTick = ToDueTick(nextNominal);
        entry.NominalDeadlineTicks = nextNominal;
        entry.DueTick = dueTick;
        _coalescedOccurrences = (long)Int128.Min(long.MaxValue, (Int128)_coalescedOccurrences + skipped);
        AddToWheel(entry);
    }

    private void ThrowIfClosed()
    {
        if (_closed)
        {
            throw new InvalidOperationException("Timer scheduling has been closed.");
        }
    }

    private long ToDueTick(long nominalDeadline)
    {
        var tickDuration = _options.TickDuration.Ticks;
        var dueTick = nominalDeadline / tickDuration + (nominalDeadline % tickDuration == 0 ? 0 : 1);
        _ = checked(dueTick * tickDuration);

        return dueTick;
    }
}
