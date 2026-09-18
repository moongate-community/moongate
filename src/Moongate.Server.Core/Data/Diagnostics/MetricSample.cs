using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Server.Core.Data.Diagnostics;

public sealed class MetricSample
{
    public string Name { get; }
    public double Value { get; }
    public string Unit { get; }
    public DiagnosticMetricType Type { get; }

    public MetricSample(string name, double value, string unit, DiagnosticMetricType type)
    {
        Name = name;
        Value = value;
        Unit = unit;
        Type = type;
    }
}
