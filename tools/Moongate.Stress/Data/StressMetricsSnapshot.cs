namespace Moongate.Stress.Data;

public sealed class StressMetricsSnapshot
{
    public int TotalClients { get; init; }

    public int LoginSucceeded { get; init; }

    public int LoginFailed { get; init; }

    public int UnexpectedDisconnects { get; init; }

    public long MovesSent { get; init; }

    public long MovesAcked { get; init; }

    public double AckLatencyP50Ms { get; init; }

    public double AckLatencyP95Ms { get; init; }

    public double AckLatencyP99Ms { get; init; }

    public int DurationSeconds { get; init; }
}
