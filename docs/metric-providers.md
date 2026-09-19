# Registering a metric provider

`DiagnosticService` (`src/Moongate.Server/Services/Diagnostics/DiagnosticService.cs`) carries this class summary: "Serially collects provider metrics and publishes the latest immutable snapshot." On the interval configured in `[diagnostics] interval_seconds` (see `docs/diagnostics.md`), it awaits every registered `IMetricProvider` in turn, off the game-loop thread, on a dedicated worker task started with `Task.Run`. Providers are awaited one at a time: the service never calls two providers concurrently, and never calls a given provider instance concurrently with itself.

Each cycle's samples are gathered into a fresh `DiagnosticSnapshot`, which replaces the previous one and is handed to `IDiagnosticService.GetSnapshot()`. The same snapshot is also published on the shared `IEventBusService` as a `DiagnosticSnapshotCollectedEvent`. When `log_metrics` is `true`, the cycle additionally writes a structured log line built from this template in `CollectOnceAsync`:

```csharp
"Diagnostic snapshot {Sequence}: {@Metrics}; failed providers: {FailedProviders}"
```

A provider joins this collector by implementing `IMetricProvider` and registering with `container.AddMetricProvider<TProvider>()`, typically from a plugin's `Register` method. See [Registering metric providers](plugins.md#registering-metric-providers) for how a plugin wires that call into `Register`; this page covers the contract the provider itself must meet, how failures and threading work, and how providers are tested.

## The sample provider

The sample plugin's counter, from `samples/Moongate.Sample.Plugin/Internal/GreetingCounter.cs`:

```csharp
namespace Moongate.Sample.Plugin.Internal;

/// <summary>Counts greetings. Incremented from the game loop by the module and from the console thread by the command; read by the diagnostics thread; hence atomic.</summary>
public sealed class GreetingCounter
{
    private long _count;

    /// <summary>Gets the number of greetings produced since the plugin was registered.</summary>
    public long Count => Interlocked.Read(ref _count);

    /// <summary>Records one greeting.</summary>
    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }
}
```

`GreeterModule.Hello` calls `_counter.Increment()` on every call, and that method runs both as a Lua binding on the game loop and, through `GreetCommand`, on the console thread. `GreetingMetricProvider.CollectAsync` reads `_counter.Count` on the diagnostics worker thread. Three threads touch this one field, which is exactly why it is backed by `Interlocked` rather than a plain `long`.

The provider itself, from `samples/Moongate.Sample.Plugin/Diagnostics/GreetingMetricProvider.cs`:

```csharp
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
        IReadOnlyList<MetricSample> samples = [new MetricSample("hello_calls", _counter.Count, "calls", DiagnosticMetricType.Counter)];

        return new ValueTask<IReadOnlyList<MetricSample>>(samples);
    }
}
```

It is registered from `samples/Moongate.Sample.Plugin/SamplePlugin.cs`:

```csharp
public void Register(Container container)
{
    container.RegisterInstance(new GreetingCounter());
    container.RegisterScriptModule<GreeterModule>();
    container.RegisterScriptEnum<Tone>();
    container.RegisterCommand<GreetCommand>(
        "greet",
        "Greets someone from the console: greet <name> [tone].",
        CommandSourceType.Console,
        AccountType.Regular
    );
    container.AddMetricProvider<GreetingMetricProvider>();
}
```

`GreetingCounter` is registered first, as an instance, so both `GreeterModule` and `GreetingMetricProvider` resolve the same shared object. Calling `CollectAsync` produces one sample: local name `hello_calls`, unit `calls`, type `Counter` — `DiagnosticService` publishes it as `greeter.hello_calls` (see [The contract](#the-contract)).

## The contract

`IMetricProvider`, from `src/Moongate.Server.Core/Interfaces/Diagnostics/IMetricProvider.cs`:

```csharp
using Moongate.Server.Core.Data.Diagnostics;

namespace Moongate.Server.Core.Interfaces.Diagnostics;

/// <summary>Collects one named group of diagnostic metrics; callers serialize collections per instance.</summary>
public interface IMetricProvider
{
    /// <summary>Gets the stable name used to qualify metrics and report provider failures.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Collects a fresh metric list. Implementations observe cancellation before accessing their source and may
    /// throw <see cref="OperationCanceledException"/>; the diagnostic service does not invoke an instance concurrently.
    /// </summary>
    ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default);
}
```

**`ProviderName`** must be stable and unique for the life of the service. `DiagnosticService`'s constructor reads every provider's name once, freezes it into an internal array, and validates it there — a later mutation of `IMetricProvider.ProviderName` on a live instance has no effect (`DiagnosticServiceTests.Constructor_FreezesProviderListAndNames` proves this by clearing the source list and renaming the provider after construction: the frozen snapshot is unaffected). The relevant part of the constructor:

```csharp
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
```

Two providers sharing a `ProviderName` never reach the collection loop: the constructor throws `ArgumentException` while the service itself is being built, so a duplicate is a startup failure, not something isolated per collection cycle the way a `CollectAsync` failure is (see [Failures](#failures) below).

**`CollectAsync`** must return a fresh list each call — the XML doc says so directly, and `GreetingMetricProvider` follows it by allocating a new one-element array literal on every call rather than caching one. It must observe cancellation before touching its source; `GreetingMetricProvider` does this with `cancellationToken.ThrowIfCancellationRequested();` as its first statement, before it reads `_counter.Count`. And it is never invoked concurrently on one instance — `DiagnosticServiceTests.Tick_DuringBlockedCollectionCoalescesWithoutOverlap` asserts `provider.MaximumConcurrency` stays at `1` even when several ticks land while a collection is still blocked.

**`MetricSample`**, from `src/Moongate.Server.Core/Data/Diagnostics/MetricSample.cs`:

```csharp
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
```

**`DiagnosticMetricType`**, from `src/Moongate.Server.Core/Types/Diagnostics/DiagnosticMetricType.cs`:

```csharp
namespace Moongate.Server.Core.Types.Diagnostics;

public enum DiagnosticMetricType
{
    Gauge,
    Counter
}
```

Use `Gauge` for a value that can move up or down between samples, such as `game_loop.queue_depth` or `system.working_set_bytes` in the built-in metrics table. Use `Counter` for a value that only accumulates since the process started, such as `game_loop.accepted_work_items_total`; `DiagnosticService` enforces the "only accumulates" half by rejecting a negative `Counter` value, but it cannot detect a counter that silently resets to a lower number and still reports a valid, non-negative value — that stays a semantic contract between the provider and its consumers (see [Common mistakes](#common-mistakes)).

`DiagnosticService` builds the key each sample appears under in `DiagnosticSnapshot.Metrics` as `ProviderName + "." + sample.Name`. `DiagnosticService.IsValidName` requires provider names and local metric names alike to match `[a-z][a-z0-9_]*`. That pattern applies to `sample.Name` the same way it applies to `ProviderName`, so the local name a provider returns cannot itself contain a `.` — it is the leaf part only, in snake_case, with the provider prefix added by the service. `GreetingMetricProvider`'s sample above is exactly this: it returns the local name `hello_calls`, and `DiagnosticService` qualifies it into the `greeter.hello_calls` key that shows up in the snapshot (see [Testing](#testing) for a test that collects it through the real service and checks that key). Local names must also be unique within one provider's own result; a repeat is rejected the same way an invalid name is (see [Failures](#failures)). Units are short nouns — `calls`, `bytes`, `seconds`, `count` — matching the built-in metrics table, not abbreviations or symbols.

## Failures

A provider that throws, or whose result fails validation, does not stop the cycle. `CollectOnceAsync` isolates each provider in its own `try`/`catch`:

```csharp
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
                    (sample.Type == DiagnosticMetricType.Counter && sample.Value < 0))
                {
                    throw new InvalidOperationException($"Provider '{name}' returned an invalid diagnostic sample.");
                }

                if (!providerMetrics.TryAdd(name + "." + sample.Name, sample))
                {
                    throw new InvalidOperationException($"Provider '{name}' returned duplicate metric '{sample.Name}'.");
                }
            }
            foreach (var metric in providerMetrics)
                metrics.Add(metric.Key, metric.Value);
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
```

A throw — including an invalid-sample or duplicate-metric `InvalidOperationException`, or an `OperationCanceledException` not tied to the collector's own shutdown token — is caught by the general `catch (Exception exception)` block: the provider's name is added to `DiagnosticSnapshot.FailedProviders`, any samples it already produced that cycle are discarded, a warning is logged with the message `"Diagnostic provider {ProviderName} failed"`, and the loop moves on to the next provider. The snapshot is still built and published, just without that provider's metrics. `DiagnosticServiceTests.CollectAsync_ProviderFailureIsIsolatedAndPreviousMetricsAreRemoved` and `CollectAsync_InvalidPayloadDiscardsEntireProvider` both confirm this: the good provider's metric is still present, `FailedProviders` names only the failing one, and — since `metrics` is rebuilt from scratch every cycle — a provider's earlier successful sample does not linger into a cycle where it then fails.

The one exception is cancellation tied to the collector's own lifetime token (service shutdown): `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` rethrows instead of isolating, so `CollectOnceAsync` itself unwinds without building or publishing a snapshot for that cycle — that is `StopAsync` tearing the worker down, not a provider failure.

## Threading

Collection runs on the diagnostics worker thread started by `Task.Run(() => RunAsync(_lifetime.Token))` — off the game loop. A provider whose metric depends on game-loop-owned state has two safe options.

The first is an atomic field the game loop, and anything else, can write without a hop: `GreetingCounter` above is the example, built on `Interlocked` because it is written from the game loop and the console thread and read from the diagnostics thread.

The second is to capture the value on the loop and hand the result across through a completion-carrying work item. `IGameLoopService.PostAsync`, from `src/Moongate.Server.Core/Interfaces/Services/IGameLoopService.cs`, only guarantees admission, not execution:

```csharp
/// <summary>Waits for inbox space and completes on acceptance, not execution.</summary>
/// <remarks>
/// Cancellation before acceptance rejects the item; later cancellation does not retract it.
/// Producers must await each call to bound their pending work; the inbox does not bound unawaited producers.
/// Calls from the loop thread are rejected to prevent waiting for the loop's own inbox.
/// </remarks>
/// <exception cref="ArgumentNullException">The work item is null.</exception>
/// <exception cref="InvalidOperationException">The loop is unavailable or the caller is on the loop thread.</exception>
/// <exception cref="OperationCanceledException">Cancellation was requested before acceptance.</exception>
ValueTask PostAsync(IGameLoopWorkItem workItem, CancellationToken cancellationToken = default);
```

Because `PostAsync` completes on acceptance rather than execution, reading a result back means the work item itself has to carry its own completion. `WorldSaveCaptureWorkItem`, from `src/Moongate.Server/Services/Persistence/Internal/WorldSaveCaptureWorkItem.cs`, is that pattern:

```csharp
using Moongate.Server.Core.Interfaces.GameLoop;

namespace Moongate.Server.Services.Persistence.Internal;

/// <summary>Reports capture failures to the save worker without faulting the game loop.</summary>
internal sealed class WorldSaveCaptureWorkItem : IGameLoopWorkItem
{
    private readonly Action _capture;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task;

    public WorldSaveCaptureWorkItem(Action capture)
    {
        _capture = capture;
    }

    public void Execute()
    {
        try
        {
            _capture();
            _completion.TrySetResult();
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
```

A provider reading loop-affine state posts a work item shaped like this one, then awaits its `Completion` task — separately from the `ValueTask` `PostAsync` returns — before returning its `MetricSample`s.

The repository's other example of a member documented safe off the loop is `IScriptEngine.GetMetrics`, from `src/Moongate.Scripting/Interfaces/IScriptEngine.cs`:

```csharp
/// <summary>Returns a snapshot of the execution counters. Unlike the other members this may be called from any thread; diagnostics collectors run off the loop.</summary>
ScriptExecutionMetrics GetMetrics();
```

Every other member of `IScriptEngine` is loop-only; `GetMetrics` is called out as the one exception, in the same terms this page uses for `GreetingCounter`. `src/Moongate.Server/Commands/ScriptCommand.cs` exercises exactly that from the console thread, for its `script metrics` command:

```csharp
private void PrintMetrics(CommandContext context)
{
    // GetMetrics is the one engine member documented as callable from any thread, so no loop hop.
    var metrics = _engine.GetMetrics();
    context.Print("Files loaded: {0}", metrics.FilesLoaded);
    context.Print("Calls started: {0}", metrics.CallsStarted);
    context.Print("Coroutines resumed: {0}", metrics.CoroutinesResumed);
    context.Print("Coroutines finished: {0}", metrics.CoroutinesFinished);
    context.Print("Coroutine errors: {0}", metrics.Errors);
    context.Print("Budget aborts: {0}", metrics.BudgetAborts);
    context.Print("Active coroutines: {0}", metrics.ActiveCoroutines);
    context.Print("String cap hits: {0}", metrics.StringCapHits);
}
```

`ScriptCommand`'s other branch, `script reload <file>`, is the contrast: reloading is loop-owned work, so it posts a `ScriptReloadWorkItem` through `_gameLoop.PostAsync` and awaits that item's own outcome, rather than calling `IScriptEngine.LoadFile` inline from the console thread.

## Testing

`tests/Moongate.Tests/TestSupport/Diagnostics/DelegateMetricProvider.cs` wraps a delegate as an `IMetricProvider`, for tests that need arbitrary or failing `CollectAsync` behavior without a bespoke class:

```csharp
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

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default) => _collect(cancellationToken);
    public void Dispose() => IsDisposed = true;
}
```

`tests/Moongate.Tests/TestSupport/Diagnostics/ControlledMetricProvider.cs` is a provider a test can pause mid-collection, to check concurrency and ordering:

```csharp
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
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var active = Interlocked.Increment(ref _active);
        lock (_concurrencyGate) _maximumConcurrency = Math.Max(active, _maximumConcurrency);
        _entered.TrySetResult(Interlocked.Increment(ref _calls));
        try
        {
            await _release.Task.WaitAsync(cancellationToken);
            return [new MetricSample("value", 42, "count", DiagnosticMetricType.Gauge)];
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    public Task<int> WaitForEntryAsync() => _entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    public void Release() => _release.TrySetResult();
}
```

It blocks inside `CollectAsync` until `Release()` is called, so a test can assert on the state of a collection that has started but not finished, and it counts `Calls` and `MaximumConcurrency` to prove the one-at-a-time guarantee from [The contract](#the-contract).

`tests/Moongate.Tests/Server/Services/Diagnostics/DiagnosticServiceTests.cs` drives the service through a fixture (`DiagnosticServiceFixture`) that wires a `ControlledMetricProvider` or `DelegateMetricProvider` into a real `DiagnosticService`, with a fake `TimeProvider` to advance the collection timer on demand and a channel that captures each published `DiagnosticSnapshotCollectedEvent`. `StartAsync_PublishesSnapshotBeforeEventAndReadsDoNotCollect` is a representative test that starts the service, releases a paused provider, and checks both the event and `GetSnapshot()` see the same snapshot:

```csharp
[Fact]
public async Task StartAsync_PublishesSnapshotBeforeEventAndReadsDoNotCollect()
{
    var provider = new ControlledMetricProvider();
    using var fixture = new DiagnosticServiceFixture([provider]);
    var received = new TaskCompletionSource<DiagnosticSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var subscription = fixture.Bus.Subscribe<DiagnosticSnapshotCollectedEvent>((message, _) =>
    {
        Assert.Same(message.Snapshot, fixture.Service.GetSnapshot());
        received.TrySetResult(message.Snapshot);
        return Task.CompletedTask;
    });
    Assert.Null(fixture.Service.GetSnapshot());
    await fixture.Service.StartAsync();
    provider.Release();
    var snapshot = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(42d, snapshot.Metrics["test.value"].Value);
    Assert.Same(snapshot, fixture.Service.GetSnapshot());
    Assert.Same(snapshot, fixture.Service.GetSnapshot());
    Assert.Equal(1, provider.Calls);
    await fixture.Service.StopAsync();
    Assert.Same(snapshot, fixture.Service.GetSnapshot());
}
```

The sample plugin's own provider is exercised end to end in `tests/Moongate.Tests/Integration/Plugins/SamplePluginTests.cs`, which resolves it from the container and collects it through a real `DiagnosticService` (via `DiagnosticServiceFixture`, the same fixture `DiagnosticServiceTests` uses) rather than calling `CollectAsync` directly — proving the qualified name from [The contract](#the-contract) round-trips through the service's own validation:

```csharp
var provider = Assert.Single(container.ResolveMany<IMetricProvider>(), candidate => candidate.ProviderName == "greeter");
using var diagnostics = new DiagnosticServiceFixture([provider]);
await diagnostics.Service.StartAsync();
var snapshot = await diagnostics.NextAsync();
Assert.Empty(snapshot.FailedProviders);
Assert.Equal(2, snapshot.Metrics["greeter.hello_calls"].Value);
```

The count is `2` because the test calls `greeter.hello` once from Lua and once more through the `greet` console command before collecting. `FailedProviders` is empty and `greeter.hello_calls` is present, so the sample's local name (`hello_calls`) passed `DiagnosticService`'s validation and was qualified into the snapshot key exactly as the naming rule predicts.

## Common mistakes

- **Returning the same mutable list on every call.** `CollectAsync` must return "a fresh metric list," per `IMetricProvider`'s own doc comment; a provider that caches and reuses one `List<MetricSample>` (or mutates it in place between calls) breaks that contract, and any code still holding an older `DiagnosticSnapshot` built from that list can see it change out from under an object that is supposed to be immutable.
- **Blocking on the loop from `CollectAsync`.** Providers are awaited one at a time, on the diagnostics worker; a provider that synchronously blocks waiting for game-loop work (instead of awaiting a work item's own `Completion`, as in [Threading](#threading)) stalls every provider queued after it for that cycle. `DiagnosticServiceTests.Tick_DuringBlockedCollectionCoalescesWithoutOverlap` shows the service's own response to a stuck collection: ticks that land while it is blocked simply coalesce into one more cycle once it unblocks, rather than overlapping — it does not run your blocked provider any sooner.
- **Provider names that collide.** Two providers sharing a `ProviderName` fail the service at construction, with `ArgumentException($"Duplicate diagnostic provider name '{name}'.")`, before any collection happens — not a per-cycle isolation like a `CollectAsync` failure.
- **Reporting a counter that resets.** `DiagnosticService` rejects a negative `Counter` value, but a counter that drops back to a smaller non-negative number (for example, one reset on every restart of some inner component instead of the process) passes validation while breaking the "accumulates since start" meaning `Counter` is documented to have. If a value can legitimately go down, it is a `Gauge`.
