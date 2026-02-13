namespace Moongate.Core.Server.Data.Metrics.Diagnostic;

public class MetricProviderData
{
    public string Name { get; set; }
    public object Value { get; set; }

    public MetricProviderData(string name, object value)
    {
        Name = name;
        Value = value;
    }

    public MetricProviderData() { }

    public override string ToString()
        => Value.ToString();
}
