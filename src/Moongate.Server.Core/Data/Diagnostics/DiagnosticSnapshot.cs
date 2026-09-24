using System.Collections.Frozen;

namespace Moongate.Server.Core.Data.Diagnostics;

public sealed class DiagnosticSnapshot
{
    public long Sequence { get; }
    public DateTimeOffset CollectedAt { get; }
    public TimeSpan CollectionDuration { get; }
    public IReadOnlyDictionary<string, MetricSample> Metrics { get; }
    public IReadOnlyCollection<string> FailedProviders { get; }

    public DiagnosticSnapshot(
        long sequence,
        DateTimeOffset collectedAt,
        TimeSpan collectionDuration,
        IReadOnlyDictionary<string, MetricSample> metrics,
        IReadOnlyCollection<string> failedProviders
    )
    {
        Sequence = sequence;
        CollectedAt = collectedAt;
        CollectionDuration = collectionDuration;
        Metrics = metrics.ToFrozenDictionary(StringComparer.Ordinal);
        FailedProviders = Array.AsReadOnly(failedProviders.ToArray());
    }
}
