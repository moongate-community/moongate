namespace Moongate.Benchmarks.Compare;

public sealed class BenchmarkRunResult
{
    public required string Name { get; init; }
    public required double MeanNanoseconds { get; init; }
    public required double AllocatedBytesPerOperation { get; init; }
}
