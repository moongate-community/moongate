namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress.Data;

internal sealed record StressOperationReport(
    long Successes,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds
);
