namespace Moongate.Stress.Data;

public sealed class StressEvaluationResult
{
    public required StressMetricsSnapshot Metrics { get; init; }

    public bool Passed => FailedConditions.Count == 0;

    public IReadOnlyList<string> FailedConditions { get; init; } = [];
}
