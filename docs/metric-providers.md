# Registering a metric provider

A metric provider is a class that hands the diagnostics service a named group of
samples on every collection cycle; a plugin author writes one to make a plugin's
counters and gauges appear in the diagnostics snapshot next to the built-in metrics.
This page covers the contract the provider must meet, how failures and threading
work, and how to test one. For the collector's configuration and the built-in
metrics, see [Diagnostics](diagnostics.md); for where the registration call sits in a
plugin, see [Registering metric providers](plugins.md#registering-metric-providers).

## The sample provider

The sample plugin counts greetings in a shared `GreetingCounter` backed by
`Interlocked`, because the game loop and the console thread write it and the
diagnostics thread reads it. The provider that reports it:

```csharp
using Moongate.Sample.Plugin.Internal;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Types.Diagnostics;

namespace Moongate.Sample.Plugin.Diagnostics;

public sealed class GreetingMetricProvider : IMetricProvider
{
    private readonly GreetingCounter _counter;

    public string ProviderName => "greeter";

    public GreetingMetricProvider(GreetingCounter counter)
    {
        _counter = counter;
    }

    public ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MetricSample> samples = [new MetricSample("hello_calls", _counter.Count, "calls", DiagnosticMetricType.Counter)];

        return new ValueTask<IReadOnlyList<MetricSample>>(samples);
    }
}
```

The plugin registers the counter as an instance first, so the Lua module and the
provider share it, then calls `container.AddMetricProvider<GreetingMetricProvider>()`.
Each collection produces one sample, `hello_calls`, which the service publishes as
`greeter.hello_calls`.

## The contract

`IMetricProvider` has two members:

```csharp
public interface IMetricProvider
{
    string ProviderName { get; }

    ValueTask<IReadOnlyList<MetricSample>> CollectAsync(CancellationToken cancellationToken = default);
}
```

**`ProviderName`** must be stable and unique. The service reads every provider's name
once when it is constructed and validates it there; changing the property on a live
instance has no effect. Two providers with the same name fail the service at
construction with `Duplicate diagnostic provider name '{name}'.`, which is a startup
failure, not a per-cycle one.

**`CollectAsync`** must return a fresh list on every call, observe the cancellation
token before touching its source, and may throw `OperationCanceledException`. The
service never calls two providers at the same time and never calls one instance
concurrently with itself.

**`MetricSample`** carries `Name`, `Value` (a `double`), `Unit` and `Type`.
`DiagnosticMetricType` has two values. Use `Gauge` for a value that can move up or
down between samples, such as `game_loop.queue_depth`. Use `Counter` for a value that
only accumulates since the process started, such as
`game_loop.accepted_work_items_total`. The service rejects a negative `Counter`, but it
cannot detect a counter that resets to a smaller non-negative number; that stays a
contract between the provider and its consumers.

**Names.** The snapshot key is `ProviderName + "." + sample.Name`. Both parts must
match `[a-z][a-z0-9_]*`, so the local name is the leaf only, in snake_case, with no
dot; the service adds the prefix. Local names must be unique within one provider's
result. Units are short nouns in the style of the built-in table: `calls`, `bytes`,
`seconds`, `count`.

## Collection and failures

On the interval in `[diagnostics] interval_seconds`, the service awaits every
registered provider in turn on its own worker thread, off the game loop. It validates
each sample (name, non-blank unit, finite value, known type, non-negative counter) and
rejects duplicates within a provider. All samples of a cycle join one fresh
`DiagnosticSnapshot`, which replaces the previous one, is returned by
`IDiagnosticService.GetSnapshot()` and is published on the event bus as
`DiagnosticSnapshotCollectedEvent`. With `log_metrics = true` the cycle also writes one
structured log line.

A provider that throws, or whose result fails validation, does not stop the cycle. Its
name is added to `DiagnosticSnapshot.FailedProviders`, any samples it produced that
cycle are discarded, a warning `Diagnostic provider {ProviderName} failed` is logged,
and collection continues with the next provider. The snapshot is still built and
published without that provider's metrics. Because the metric set is rebuilt every
cycle, an earlier successful sample does not linger into a cycle where the provider
fails.

The one exception is cancellation from the service's own shutdown token: that unwinds
the cycle without building or publishing a snapshot, because it is the service
stopping, not a provider failing.

## Threading

Collection runs off the game loop. A provider whose metric depends on loop-owned
state has two safe options:

- **An atomic field** that the loop and any other thread can write without a hop, as
  `GreetingCounter` does with `Interlocked`.
- **A work item posted to the loop.** `IGameLoopService.PostAsync` completes on
  acceptance, not on execution, so the work item must carry its own completion
  (a `TaskCompletionSource` set from `Execute()`). The provider awaits that completion,
  separately from the `ValueTask` that `PostAsync` returns, before returning its
  samples. See [Game loop and timers](game-loop-and-timers.md).

Never block synchronously on loop work inside `CollectAsync`: providers run one at a
time, so a stuck provider delays every provider queued after it for that cycle.

## Testing

`tests/Moongate.Tests/TestSupport/Diagnostics/` holds a delegate-backed provider and
a pausable one. Wire either into a real `DiagnosticService` with a fake
`TimeProvider`, advance the timer, then assert on `GetSnapshot()` and the event.

The sample plugin's provider is exercised end to end in
[Testing a plugin](plugins.md#testing-a-plugin): the test loads the plugin through
the real loader, calls the greeter once from Lua and once from the console command,
collects through the service, and checks that `greeter.hello_calls` equals `2` with
an empty `FailedProviders`.

## Common mistakes

- **Returning the same mutable list on every call.** Code holding an older snapshot
  built from that list can see it change under an object that is meant to be
  immutable. Allocate a new list per call.
- **Blocking on the loop from `CollectAsync`.** Ticks that land while a collection is
  stuck coalesce into one later cycle; they do not run the blocked provider sooner.
  Await a work item's own completion instead.
- **Provider names that collide.** The service fails at construction, before any
  collection happens.
- **Reporting a counter that resets.** A counter that drops to a smaller non-negative
  number passes validation while breaking the "accumulates since start" meaning. If a
  value can legitimately go down, it is a `Gauge`.
