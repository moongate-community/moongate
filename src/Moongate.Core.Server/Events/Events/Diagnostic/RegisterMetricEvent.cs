using Moongate.Core.Interfaces.Metrics;

namespace Moongate.Core.Server.Events.Events.Diagnostic;

public record RegisterMetricEvent(IMetricsProvider provider);
