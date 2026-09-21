using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class DelegateMetricProvider : IMetricProvider, IDisposable
{
    private readonly Func<CancellationToken, ValueTask<IReadOnlyList<MetricSample>>> _collect;
    public string ProviderName { get; set; }
    public bool IsDisposed { get; private set; }

    public DelegateMetricProvider(string name, Func<CancellationToken, ValueTask<IReadOnlyList<MetricSample>>> collect)
    {
        ProviderName = name;
        _collect = collect;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default) =>
        _collect(cancellationToken);

    public void Dispose() => IsDisposed = true;
}
