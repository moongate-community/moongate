# Game loop

Moongate exposes the runtime loop through `IGameLoopContext`. This keeps server and scripting code coupled to a narrow façade rather than separately depending on the dispatcher and timer service everywhere.

## Dispatcher and timers

`GameLoopContext` delegates directly to the two interfaces registered during bootstrap:

- `Post` calls `IMainThreadDispatcher.Post`. The interface contract states that the action runs on the game-loop thread on the next frame.
- `Schedule` registers a non-repeating timer for the supplied delay and returns its id.
- `ScheduleRepeating` registers a repeating timer with an interval and optional initial delay, and returns its id.
- `Cancel` unregisters a timer by id and returns whether a timer was removed.

No stronger ordering, precision, or concurrent-callback guarantees are asserted here because those details are not established by Moongate's interface comments or adapter implementation.

Inbound networking uses the dispatcher directly: it copies each received frame and posts packet processing so handler-driven session and game-state work is marshalled onto the game-loop thread.

## Startup timer

`Program.cs` subscribes to `EngineStartedEvent`. When that event is delivered, it resolves `TimerAutostartService` and calls `InitDefaultTimers`. The current default is `persistence_save`, registered with a 300-second interval; its async callback logs timing and requests a persistence snapshot.

## Lua boundary

`GameLoopModule` publishes the façade as the Lua `game` module:

```text
game.post(callback)
game.schedule(name, delayMs, callback) -> timer id
game.schedule_repeating(name, intervalMs, callback) -> timer id
game.cancel(timerId) -> boolean
```

Posted and scheduled closures are wrapped before being passed to `IGameLoopContext`. The wrapper calls the MoonSharp closure and catches/logs exceptions so a Lua callback exception does not escape through the wrapper. Millisecond numbers are converted with `TimeSpan.FromMilliseconds`.

The scripting test proves that `game.post` defers execution until the dispatcher is drained, and that a scheduled timer returns an id accepted by `game.cancel`. Repeating execution timing is defined by the façade and adapter but is not directly exercised by that test.

## Source map

### Runtime

- `src/Moongate.Core/Interfaces/IGameLoopContext.cs`
- `src/Moongate.Server/Services/Game/GameLoopContext.cs`
- `src/Moongate.Server/Services/Network/NetworkService.cs`
- `src/Moongate.Server/Autostart/TimerAutostartService.cs`
- `src/Moongate.Server/Program.cs`
- `src/Moongate.Scripting/Modules/GameLoopModule.cs`

### Tests

- `tests/Moongate.Tests/Scripting/GameLoopModuleTests.cs`
