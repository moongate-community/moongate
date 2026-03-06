using System.Collections.Concurrent;
using System.Diagnostics;
using Moongate.Stress.Data;

namespace Moongate.Stress.Services;

public sealed class StressMetricsCollector
{
    private readonly ConcurrentDictionary<(int ClientIndex, byte Sequence), long> _moveStartTicks = new();
    private readonly ConcurrentQueue<double> _ackLatenciesMs = new();

    private int _loginSucceeded;
    private int _loginFailed;
    private int _unexpectedDisconnects;
    private long _movesSent;
    private long _movesAcked;

    public StressMetricsSnapshot CreateSnapshot(int totalClients, TimeSpan duration)
    {
        var latencies = _ackLatenciesMs.ToArray();
        Array.Sort(latencies);

        return new()
        {
            TotalClients = totalClients,
            LoginSucceeded = Volatile.Read(ref _loginSucceeded),
            LoginFailed = Volatile.Read(ref _loginFailed),
            UnexpectedDisconnects = Volatile.Read(ref _unexpectedDisconnects),
            MovesSent = Volatile.Read(ref _movesSent),
            MovesAcked = Volatile.Read(ref _movesAcked),
            AckLatencyP50Ms = Percentile(latencies, 0.50),
            AckLatencyP95Ms = Percentile(latencies, 0.95),
            AckLatencyP99Ms = Percentile(latencies, 0.99),
            DurationSeconds = (int)Math.Round(duration.TotalSeconds)
        };
    }

    public void MarkLoginFailed()
        => _ = Interlocked.Increment(ref _loginFailed);

    public void MarkLoginSucceeded()
        => _ = Interlocked.Increment(ref _loginSucceeded);

    public void MarkMoveAcked(int clientIndex, byte sequence)
    {
        if (_moveStartTicks.TryRemove((clientIndex, sequence), out var startedTick))
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - startedTick;
            var elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            _ackLatenciesMs.Enqueue(elapsedMs);
        }

        _ = Interlocked.Increment(ref _movesAcked);
    }

    public void MarkMoveSent(int clientIndex, byte sequence)
    {
        _ = Interlocked.Increment(ref _movesSent);
        _moveStartTicks[(clientIndex, sequence)] = Stopwatch.GetTimestamp();
    }

    public void MarkUnexpectedDisconnect()
        => _ = Interlocked.Increment(ref _unexpectedDisconnects);

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;

        if (index < 0)
        {
            index = 0;
        }

        if (index >= sortedValues.Count)
        {
            index = sortedValues.Count - 1;
        }

        return sortedValues[index];
    }
}
