using Moongate.Core.Server.Data.Metrics.Diagnostic;

namespace Moongate.Core.Server.Events.Events.Diagnostic;

public record DiagnosticMetricEvent(MetricProviderData Metrics);
