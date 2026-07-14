# Scripting

Moongate's scripting plugin configures the SquidStd Lua engine and exposes two Moongate-owned modules: logging and game-loop dispatch. This is a deliberately narrow bridge; no gameplay-object API is registered in this project.

## Engine and module registration

`MoongateScriptingPlugin.Configure` resolves the application metadata and directory configuration, then registers a Lua engine whose root and script directory both point to the configured `scripts` path. It supplies the host application name and version, registers Lua event integration, and registers `LoggerModule` and `GameLoopModule`.

Nothing in the plugin or modules establishes hot reload, isolation, sandboxing, or filesystem-access policy. Those properties must not be assumed from the presence of a Lua engine.

## Exposed capabilities

The `log` module forwards `info`, `warn`, `error`, and `debug` calls to Serilog, accepting a message and additional argument values.

The `game` module adapts `IGameLoopContext`:

```text
game.post(callback)
game.schedule(name, delayMs, callback) -> timer id
game.schedule_repeating(name, intervalMs, callback) -> timer id
game.cancel(timerId) -> boolean
```

Delays and intervals are converted from milliseconds to `TimeSpan`. Each closure is invoked through a wrapper that catches and logs its exception. The module test proves that posted work remains deferred until the main-thread dispatcher drains and that a scheduled timer id can be cancelled. The lower-level dispatch and timer contracts are described on the [game loop](./game-loop.md) page.

## Source map

### Runtime

- `src/Moongate.Scripting/MoongateScriptingPlugin.cs`
- `src/Moongate.Scripting/Modules/LoggerModule.cs`
- `src/Moongate.Scripting/Modules/GameLoopModule.cs`
- `src/Moongate.Core/Interfaces/IGameLoopContext.cs`
- `src/Moongate.Server/Services/Game/GameLoopContext.cs`

### Tests

- `tests/Moongate.Tests/Scripting/GameLoopModuleTests.cs`

