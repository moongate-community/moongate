namespace Moongate.Server.Data.Internal.Diagnostics;

internal sealed record ProcessMetricsReading
{
    public int ProcessId { get; init; }
    public int ProcessorCount { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public TimeSpan TotalProcessorTime { get; init; }
    public long WorkingSetBytes { get; init; }
    public long PrivateMemoryBytes { get; init; }
    public long ManagedMemoryBytes { get; init; }
    public int ThreadCount { get; init; }
    public int GcGen0Collections { get; init; }
    public int GcGen1Collections { get; init; }
    public int GcGen2Collections { get; init; }
}
