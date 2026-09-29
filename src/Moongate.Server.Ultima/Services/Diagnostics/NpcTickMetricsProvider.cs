using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services.Diagnostics;

/// <summary>
///     The <c>npcs</c> metrics: how many NPCs are awake near players and how many thinks ran.
/// </summary>
public sealed class NpcTickMetricsProvider : IMetricProvider
{
    private readonly INpcTickService _ticks;

    public string ProviderName => "npcs";

    public NpcTickMetricsProvider(INpcTickService ticks)
    {
        _ticks = ticks;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetricSample> samples =
        [
            new("awake", _ticks.AwakeCount, "count", DiagnosticMetricType.Gauge),
            new("thinks_total", _ticks.ThinkCount, "count", DiagnosticMetricType.Counter)
        ];

        return ValueTask.FromResult(samples);
    }
}
