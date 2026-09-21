using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Diagnostics;
using Serilog;

namespace Moongate.Server.Services.Diagnostics;

/// <summary>Serially collects provider metrics and publishes the latest immutable snapshot.</summary>
public sealed class DiagnosticService : IDiagnosticService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<DiagnosticService>();
    private readonly (string Name, IMetricProvider Provider)[] _providers;
    private readonly DiagnosticOptions _options;
    private readonly IEventBusService _eventBus;
    private readonly TimeProvider _timeProvider;
    private readonly BootstrapLifecycleTasks _lifecycle = new();
    private readonly Lock _lifecycleGate = new();
    private readonly AsyncLocal<bool> _workerContext = new();
    private readonly CancellationTokenSource _lifetime;

    private DiagnosticSnapshot? _snapshot;
    private Task? _worker;
    private long _sequence;
    private bool _stopping;
    private bool _disposed;

    public const int StartupPriority = 900;

    public DiagnosticService(
        IEnumerable<IMetricProvider> providers,
        DiagnosticOptions options,
        IEventBusService eventBus,
        TimeProvider timeProvider
    )
    {
        _options = new()
        {
            Enabled = options.Enabled,
            Interval = options.Interval,
            LogMetrics = options.LogMetrics
        };
        _eventBus = eventBus;
        _timeProvider = timeProvider;
        _providers = providers.Select(provider => (provider.ProviderName, provider)).ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (name, _) in _providers)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    $"Invalid diagnostic provider name '{name}'; expected [a-z][a-z0-9_]*.",
                    nameof(providers)
                );
            }

            if (!names.Add(name))
            {
                throw new ArgumentException($"Duplicate diagnostic provider name '{name}'.", nameof(providers));
            }
        }

        _lifetime = new();
    }

    /// <inheritdoc />
    public DiagnosticSnapshot? GetSnapshot()
        => Volatile.Read(ref _snapshot);

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_lifecycleGate)
        {
            if (_stopping)
            {
                throw new InvalidOperationException("Diagnostics cannot start after shutdown begins.");
            }

            return _lifecycle.StartAsync(StartCoreAsync);
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        ThrowIfWorkerContext();

        lock (_lifecycleGate)
        {
            _stopping = true;

            return _lifecycle.StopAsync(StopCoreAsync);
        }
    }

    private async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        var started = _timeProvider.GetTimestamp();
        var metrics = new Dictionary<string, MetricSample>(StringComparer.Ordinal);
        var failedProviders = new List<string>();

        foreach (var (name, provider) in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var samples = await provider.CollectAsync(cancellationToken).ConfigureAwait(false);
                var providerMetrics = new Dictionary<string, MetricSample>(StringComparer.Ordinal);

                foreach (var sample in samples)
                {
                    if (sample is null ||
                        !IsValidName(sample.Name) ||
                        string.IsNullOrWhiteSpace(sample.Unit) ||
                        !double.IsFinite(sample.Value) ||
                        sample.Type is not (DiagnosticMetricType.Gauge or DiagnosticMetricType.Counter) ||
                        sample.Type == DiagnosticMetricType.Counter && sample.Value < 0)
                    {
                        throw new InvalidOperationException($"Provider '{name}' returned an invalid diagnostic sample.");
                    }

                    if (!providerMetrics.TryAdd(name + "." + sample.Name, sample))
                    {
                        throw new InvalidOperationException($"Provider '{name}' returned duplicate metric '{sample.Name}'.");
                    }
                }

                foreach (var metric in providerMetrics)
                {
                    metrics.Add(metric.Key, metric.Value);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedProviders.Add(name);
                _logger.Warning(exception, "Diagnostic provider {ProviderName} failed", name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = new DiagnosticSnapshot(
            ++_sequence,
            _timeProvider.GetUtcNow(),
            _timeProvider.GetElapsedTime(started),
            metrics,
            failedProviders
        );
        Volatile.Write(ref _snapshot, snapshot);

        if (_options.LogMetrics)
        {
            _logger.Information(
                "Diagnostic snapshot {Sequence}: {@Metrics}; failed providers: {FailedProviders}",
                snapshot.Sequence,
                snapshot.Metrics,
                snapshot.FailedProviders
            );
        }

        await _eventBus.PublishAsync(new DiagnosticSnapshotCollectedEvent(snapshot), cancellationToken)
                       .ConfigureAwait(false);
    }

    private static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (var character in name)
        {
            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_')
            {
                return false;
            }
        }

        return true;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _workerContext.Value = true;

        try
        {
            // Create before the immediate collection so ticks during it coalesce deterministically.
            using var timer = new PeriodicTimer(_options.Interval, _timeProvider);

            do
            {
                await CollectOnceAsync(cancellationToken).ConfigureAwait(false);
            } while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected shutdown; StopAsync joins this worker.
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Diagnostic worker failed");

            throw;
        }
        finally
        {
            _workerContext.Value = false;
        }
    }

    private Task StartCoreAsync()
    {
        try
        {
            _options.Validate();

            if (_options.Enabled)
            {
                _logger.Information(
                    "Starting diagnostics with {ProviderCount} providers at interval {Interval}",
                    _providers.Length,
                    _options.Interval
                );
                _worker = Task.Run(() => RunAsync(_lifetime.Token));
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            // BootstrapLifecycleTasks must receive a task even for synchronous startup failures.
            return Task.FromException(exception);
        }
    }

    private async Task StopCoreAsync(Task? startup)
    {
        try
        {
            try
            {
                await _lifetime.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                if (startup is not null)
                {
                    await startup.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }

                if (_worker is not null)
                {
                    await _worker.ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _logger.Information("Diagnostics stopped");
        }
    }

    private void ThrowIfWorkerContext()
    {
        if (_workerContext.Value)
        {
            throw new InvalidOperationException(
                "Diagnostic providers and observers cannot stop or dispose their own collector."
            );
        }
    }

    public void Dispose()
    {
        ThrowIfWorkerContext();

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            // Stop joins the worker even when cancellation callbacks or the worker itself fail.
            lock (_lifecycleGate)
            {
                if (!_disposed)
                {
                    _lifetime.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
