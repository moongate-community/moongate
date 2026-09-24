using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Server.Services.Diagnostics.Providers;

public sealed class SessionMetricsProvider : IMetricProvider
{
    private readonly ISessionService _sessions;

    public string ProviderName => "sessions";

    public SessionMetricsProvider(ISessionService sessions)
    {
        _sessions = sessions;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetricSample> samples =
        [
            new("registered_sessions", _sessions.Count, "count", DiagnosticMetricType.Gauge)
        ];

        return ValueTask.FromResult(samples);
    }
}
