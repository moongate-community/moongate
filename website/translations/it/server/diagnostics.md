<!-- translation: {"sourceHash":"c1ec8d15faddbb377d4509ca30880ac838b28d9a0f2927c71bd85e1da4a0ddcc","title":"Diagnostica"} -->

# Diagnostica

Moongate raccoglie uno snapshot immutabile in memoria delle metriche del processo e del server. Il collector si avvia con il server, opera fuori dal thread del game loop e pubblica ogni snapshot completato tramite il bus di eventi condiviso. Non espone un endpoint HTTP né conserva la cronologia degli snapshot.

## Configurazione

Aggiungi la seguente sezione a `config/moongate.toml`:

```toml
[diagnostics]
enabled = true
interval_seconds = 5
log_metrics = false
```

`enabled` controlla se il worker si avvia. `interval_seconds` accetta secondi interi e deve produrre un intervallo di `PeriodicTimer` tra 1 e 4.294.967.294 millisecondi, anche quando la diagnostica è disabilitata. `log_metrics` scrive ogni snapshot completato nel log del server. I file di configurazione esistenti senza questa sezione mantengono i valori predefiniti mostrati sopra e non vengono riscritti.

`IDiagnosticService.GetSnapshot()` restituisce `null` finché la prima raccolta non termina. Rimane inoltre `null` mentre la diagnostica è disabilitata. Leggere uno snapshot non attiva mai la raccolta né cambia i valori di riferimento CPU o GC:

```csharp
var diagnostics = container.Resolve<IDiagnosticService>();
var snapshot = diagnostics.GetSnapshot();

if (snapshot is not null &&
    snapshot.Metrics.TryGetValue("system.working_set_bytes", out var workingSet))
{
    Console.WriteLine($"Working set: {workingSet.Value} {workingSet.Unit}");
}
```

Ogni campione ha un `Value` numerico, una `Unit` e un `Type` di `Gauge` o `Counter`. Le chiavi delle metriche combinano il nome del provider e quello locale del campione con un punto.

## Metriche integrate

| Chiave | Tipo | Unità |
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
| `npcs.awake` | Gauge | `count` |
| `npcs.thinks_total` | Counter | `count` |
| `sessions.registered_sessions` | Gauge | `count` |

L'uso della CPU viene calcolato come:

```text
100 × change in cumulative process CPU time
──────────────────────────────────────────
elapsed monotonic time × ProcessorCount
```

Il risultato viene limitato a 0–100 percento. Il primo snapshot stabilisce il riferimento CPU, quindi omette `system.cpu_usage_percent`; non viene riportato come zero. `Environment.ProcessorCount` è la capacità logica esposta dal runtime. In un container riflette l'interpretazione dei limiti CPU da parte del runtime e può arrotondare una quota frazionaria, quindi la percentuale non è una misura esatta di un'allocazione frazionaria del container. Il contatore cumulativo del tempo CPU è disponibile per chi necessita di un altro calcolo.

## Errori ed eventi

Il fallimento di un provider non interrompe la raccolta. Le sue metriche vengono omesse da quello snapshot e il suo nome compare in `DiagnosticSnapshot.FailedProviders`. Gli altri provider continuano normalmente. Gli snapshot e le loro collezioni di metriche sono immutabili.

Sottoscrivi `DiagnosticSnapshotCollectedEvent` sul `IEventBusService` condiviso. Conserva e rilascia il token restituito quando il sottoscrittore non è più attivo:

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

Gli handler vengono eseguiti sul worker della diagnostica. Un handler che deve modificare entità o altro stato di gioco deve inviare lavoro tramite `IGameLoopService`; non deve modificare direttamente lo stato di proprietà del game loop.

## Provider dei plugin

I provider implementano `IMetricProvider` e vengono registrati con `AddMetricProvider<T>()` dal metodo `Register` di un plugin oppure da `Program.cs`. Vedi [`metric-providers.md`](metric-providers.md) per contratto, threading e test.

Vedi [Game loop e timer](game-loop-and-timers.md) per le impostazioni di coda, batch e timer
alla base delle metriche di runtime.
