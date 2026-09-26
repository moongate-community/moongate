using Moongate.Server.Core.Data.Diagnostics;

namespace Moongate.Server.Core.Interfaces.Diagnostics;

/// <summary>
///     Collects one named group of diagnostic metrics; callers serialize collections per instance.
/// </summary>
public interface IMetricProvider
{
    /// <summary>
    ///     Gets the stable name used to qualify metrics and report provider failures.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    ///     Collects a fresh metric list. Implementations observe cancellation before accessing their source and may
    ///     throw <see cref="OperationCanceledException" />; the diagnostic service does not invoke an instance concurrently.
    /// </summary>
    ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default);
}
