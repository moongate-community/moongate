# Diagnostics

Moongate collects an immutable in-memory snapshot of process and server metrics. The collector starts with the server, runs outside the game-loop thread, and publishes each completed snapshot through the shared event bus. It does not expose an HTTP endpoint or retain snapshot history.

## Configuration

Add the following section to `config/moongate.toml`:

```toml
[diagnostics]
enabled = true
interval_seconds = 5
log_metrics = false
```

`enabled` controls whether the worker starts. `interval_seconds` accepts whole seconds and must produce a `PeriodicTimer` interval between 1 and 4,294,967,294 milliseconds, even when diagnostics are disabled. `log_metrics` writes every completed snapshot to the server log. Existing configuration files without this section keep the defaults shown above and are not rewritten.

`IDiagnosticService.GetSnapshot()` returns `null` until the first collection finishes. It also remains `null` while diagnostics are disabled. Reading a snapshot never triggers collection or changes CPU or GC baselines:

```csharp
var diagnostics = container.Resolve<IDiagnosticService>();
var snapshot = diagnostics.GetSnapshot();

if (snapshot is not null &&
    snapshot.Metrics.TryGetValue("system.working_set_bytes", out var workingSet))
{
    Console.WriteLine($"Working set: {workingSet.Value} {workingSet.Unit}");
}
```

Each sample has a numeric `Value`, a `Unit`, and a `Type` of `Gauge` or `Counter`. Metric keys combine the provider name and local sample name with a dot.

## Built-in metrics

| Key | Type | Unit |
| --- | --- | --- |
| `system.process_id` | Gauge | `count` |
| `system.uptime_seconds` | Gauge | `seconds` |
| `system.working_set_bytes` | Gauge | `bytes` |
| `system.private_memory_bytes` | Gauge | `bytes` |
| `system.managed_memory_bytes` | Gauge | `bytes` |
| `system.thread_count` | Gauge | `count` |
| `system.processor_count` | Gauge | `count` |
| `system.cpu_usage_percent` | Gauge | `percent` |
| `system.cpu_time_seconds_total` | Counter | `seconds` |
| `system.gc_gen0_collections_total` | Counter | `count` |
| `system.gc_gen1_collections_total` | Counter | `count` |
| `system.gc_gen2_collections_total` | Counter | `count` |
| `game_loop.queue_depth` | Gauge | `count` |
| `game_loop.oldest_queued_item_age_seconds` | Gauge | `seconds` |
| `game_loop.accepted_work_items_total` | Counter | `count` |
| `game_loop.rejected_work_items_total` | Counter | `count` |
| `game_loop.executed_work_items_total` | Counter | `count` |
| `game_loop.faults_total` | Counter | `count` |
| `game_loop.last_batch_duration_seconds` | Gauge | `seconds` |
| `game_loop.max_handler_duration_seconds` | Gauge | `seconds` |
| `timers.active_timers` | Gauge | `count` |
| `timers.registered_timers_total` | Counter | `count` |
| `timers.executed_callbacks_total` | Counter | `count` |
| `timers.callback_faults_total` | Counter | `count` |
| `timers.coalesced_occurrences_total` | Counter | `count` |
| `timers.max_lateness_seconds` | Gauge | `seconds` |
| `timers.max_callback_duration_seconds` | Gauge | `seconds` |
| `timers.last_batch_duration_seconds` | Gauge | `seconds` |
| `sessions.registered_sessions` | Gauge | `count` |

CPU usage is calculated as:

```text
100 × change in cumulative process CPU time
──────────────────────────────────────────
elapsed monotonic time × ProcessorCount
```

The result is clamped to 0–100 percent. The first snapshot establishes the CPU baseline, so it omits `system.cpu_usage_percent`; it is not reported as zero. `Environment.ProcessorCount` is the logical capacity exposed by the runtime. In a container it reflects the runtime's interpretation of CPU limits and may round a fractional quota, so the percentage is not an exact measurement of a fractional container allocation. The cumulative CPU-time counter is available for consumers that need another calculation.

## Failures and events

A provider failure does not stop the collection. Its metrics are omitted from that snapshot and its name appears in `DiagnosticSnapshot.FailedProviders`. Other providers continue normally. Snapshots and their metric collections are immutable.

Subscribe to `DiagnosticSnapshotCollectedEvent` on the shared `IEventBusService`. Keep and dispose the returned token when the subscriber is no longer active:

```csharp
var bus = container.Resolve<IEventBusService>();
IDisposable subscription = bus.Subscribe<DiagnosticSnapshotCollectedEvent>((message, cancellationToken) =>
{
    Console.WriteLine($"Snapshot {message.Snapshot.Sequence} collected");
    return Task.CompletedTask;
});

// During subscriber shutdown:
subscription.Dispose();
```

Handlers run on the diagnostics worker. A handler that needs to change entities or other game state must post work through `IGameLoopService`; it must not mutate game-loop-owned state directly.

## Plugin providers

Providers implement `IMetricProvider` and are registered with `AddMetricProvider<T>()` from a plugin's `Register` method or from `Program.cs`. See [`metric-providers.md`](metric-providers.md) for the contract, threading and testing.

See [Game loop and timers](game-loop-and-timers.md) for the queue, batch and timer
settings behind the runtime metrics.
