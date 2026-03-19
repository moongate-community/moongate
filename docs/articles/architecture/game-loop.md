# Game Loop System

The game loop runs on a dedicated background thread and drives packet dispatch, timers, and outbound flush.

## Loop Responsibilities

Per iteration, `GameLoopService` does:

1. `DrainPacketQueue()`
2. `_timerService.UpdateTicksDelta(timestampMilliseconds)`
3. `DrainOutgoingPacketQueue()`

If idle CPU throttling is enabled and no work was done, it sleeps for `IdleSleepMilliseconds`.

## Timing Model

- Loop timestamp is derived from `Stopwatch.GetTimestamp()` converted to milliseconds.
- Timer progression is delta-based (not fixed-sleep-tick based).
- `TimerWheelService` accumulates elapsed milliseconds and processes due slots.

## Timer Wheel

`TimerWheelService` features:

- hashed wheel buckets
- named timers
- register/unregister by id and by name
- repeating and one-shot timers
- callback execution metrics and error counting

Used by persistence for autosave timer `db_save`.

## Outbound Flush

Outbound network send is intentionally inside the game loop:

- dequeue `OutgoingGamePacket`
- resolve session from `IGameNetworkSessionService`
- call `IOutboundPacketSender.Send(...)`

This keeps outbound ordering tied to loop progression.

## Background Job Service

`IBackgroundJobService` adds a safe two-phase model:

1. run work on background workers
2. marshal state updates back to game loop thread

Available APIs:

- `EnqueueBackground(Action|Func<Task>)`
- `RunBackgroundAndPostResult<TResult>(...)`
- `RunBackgroundAndPostResultAsync<TResult>(...)`
- `PostToGameLoop(Action)`
- `ExecutePendingOnGameLoop(...)` (drained by `GameLoopService` every tick)

Practical rule:

- worker thread: do I/O / CPU only
- game-loop callback: apply runtime world/session/entity mutations

Example:

```csharp
_backgroundJobService.RunBackgroundAndPostResult(
    () => BuildNavigationChunk(chunkId),
    navChunk =>
    {
        // Executed on game-loop thread
        _navigationService.AttachChunk(navChunk);
    },
    ex =>
    {
        _logger.Error(ex, "Navigation chunk build failed.");
    }
);
```

This preserves deterministic world-state updates while still using parallelism for heavy tasks.

The login path uses the same boundary explicitly:

- `CharacterHandler` completes the packet-critical bootstrap first
- `PlayerCharacterLoggedInEvent` is then handled by `PlayerLoginWorldSyncHandler`
- `PlayerLoginWorldSyncService` performs the login-specific mini snapshot and visible-range refill

This keeps login bootstrap policy out of the generic movement and teleport flow handled by `MobileHandler`.

## Metrics

`GameLoopService` exposes:

- tick count
- uptime
- average/max tick duration
- idle sleep count
- average work units
- outbound queue depth
- total outbound packets

Timer metrics are exposed separately by `ITimerMetricsSource`.

---

**Previous**: [Network System](network.md) | **Next**: [Session Management](sessions.md)
