using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class ControlledMetricProvider : IMetricProvider
{
    private readonly Lock _concurrencyGate = new();
    private readonly TaskCompletionSource _release;
    private readonly TaskCompletionSource<int> _entered;
    private int _active;
    private int _calls;
    private int _maximumConcurrency;

    public string ProviderName { get; }
    public int Calls => Volatile.Read(ref _calls);
    public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

    public ControlledMetricProvider(string providerName = "test")
    {
        ProviderName = providerName;
        _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var active = Interlocked.Increment(ref _active);

        lock (_concurrencyGate)
        {
            _maximumConcurrency = Math.Max(active, _maximumConcurrency);
        }
        _entered.TrySetResult(Interlocked.Increment(ref _calls));

        try
        {
            await _release.Task.WaitAsync(cancellationToken);

            return [new("value", 42, "count", DiagnosticMetricType.Gauge)];
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    public void Release()
        => _release.TrySetResult();

    public Task<int> WaitForEntryAsync()
        => _entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
}
