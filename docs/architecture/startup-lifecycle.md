# Startup lifecycle

`Moongate.Server` is the composition root. `Program.cs` performs setup in a fixed order and then hands control to the SquidStd bootstrap through its public API.

## Bootstrap order

```text
resolve root directory and CLI/default UO directory
  -> optionally print the embedded header
  -> load YAML configuration
  -> overwrite its UO directory with the resolved CLI/default value
  -> validate that an Ultima directory is configured
  -> create bootstrap metadata
  -> configure logging
  -> register plugins
  -> register Moongate services and SquidStd-facing services
  -> subscribe to EngineStartedEvent
  -> run the host until cancellation
```

The root directory comes from the command argument, then `MOONGATE_ROOT`, then a `moongate_root` directory under the current directory. Before configuration is loaded, `uoDirectory` is resolved from the command argument or defaults to `~/uo`. That resolved value is non-empty in the ordinary path, so after loading YAML and obtaining its `MoongateConfig`, `Program.cs` unconditionally replaces the YAML `UltimaDirectory` with the resolved CLI value or the resolved `~/uo` default. The mutated configuration section is then registered in the container.

Logging is configured before plugin registration so plugin-load and later startup messages can be emitted. The plugin list contains plugins discovered from `plugins` plus the built-in persistence, scripting, and data-loader plugins. This is registration order only; no claim is made here about plugin internals or their lifecycle order.

## Service composition

The server registers singleton account, character, mobile-factory, session, game-loop, and packet-handler services. The pending-login store is created as a specific singleton instance with a 30-second TTL based on `Environment.TickCount64`. The network service is registered as a lifecycle service.

Through visible SquidStd registration methods, the bootstrap also requests a main-thread dispatcher, timer wheel, event loop, job system, and event bus. Their configuration is visible—such as a 1 ms idle sleep, a 250 ms slow-tick threshold, and a five-second job shutdown timeout—but their internal startup mechanics are outside Moongate.

After the event bus is registered, `Program.cs` subscribes to `EngineStartedEvent`. That subscription resolves `TimerAutostartService` and initializes the default persistence snapshot timer. The timer registers a callback at a 300-second interval; the implementation then saves a persistence snapshot when invoked.

Finally, `RunAsync` starts and owns the host until the supplied cancellation token ends it. UO client-file discovery is not performed directly in `Program.cs`; the data-loader plugin supplies the startup service responsible for that work.

## Source map

### Runtime

- `src/Moongate.Server/Program.cs`
- `src/Moongate.Server/Autostart/TimerAutostartService.cs`
- `src/Moongate.Server/Services/Accounts/PendingLoginStore.cs`

### Tests

- `tests/Moongate.Tests/Server/PendingLoginStoreTests.cs`
