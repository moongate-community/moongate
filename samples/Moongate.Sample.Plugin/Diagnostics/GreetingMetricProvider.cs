using Moongate.Sample.Plugin.Internal;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Sample.Plugin.Diagnostics;

/// <summary>Reports how many greetings the plugin produced; the diagnostics service publishes it as <c>greeter.hello_calls</c>.</summary>
public sealed class GreetingMetricProvider : IMetricProvider
{
    private readonly GreetingCounter _counter;

    /// <inheritdoc />
    public string ProviderName => "greeter";

    /// <summary>Initializes a new instance of the <see cref="GreetingMetricProvider"/> class.</summary>
    /// <param name="counter">The counter the module increments.</param>
    public GreetingMetricProvider(GreetingCounter counter)
    {
        _counter = counter;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetricSample> samples =
            [new MetricSample("hello_calls", _counter.Count, "calls", DiagnosticMetricType.Counter)];

        return new ValueTask<IReadOnlyList<MetricSample>>(samples);
    }
}
