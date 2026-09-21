namespace Moongate.Persistence.Tests.TestSupport.Persistence.Stress.Data;

internal sealed record StressPhaseReport(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    int Sessions,
    int MaxConcurrency,
    double ElapsedSeconds,
    double SuccessfulOperationsPerSecond,
    long CommittedTransactions,
    long RolledBackTransactions,
    long AllocatedBytes,
    double ProcessCpuSeconds,
    long ProcessWorkingSetBytes,
    bool Verified,
    IReadOnlyDictionary<string, StressOperationReport> Operations,
    StressOperationReport AdmissionWait,
    IReadOnlyDictionary<string, long> Errors
);
