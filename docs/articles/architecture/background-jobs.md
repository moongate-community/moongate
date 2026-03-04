# Background Jobs

`IBackgroundJobService` provides a controlled way to run expensive work off the game-loop thread and safely apply results back on the game loop.

## Purpose

Use this service when you need parallelism for I/O or CPU tasks but must keep world-state mutations deterministic.

Two-phase flow:

1. Execute background work on worker threads.
2. Post a continuation to the game loop thread.

## Threading Contract

- Background phase: no direct mutation of runtime world/session/entity state.
- Game-loop phase: apply all mutations via callbacks executed by `GameLoopService`.

`GameLoopService.ProcessTick()` drains pending main-thread callbacks with:

- `IBackgroundJobService.ExecutePendingOnGameLoop(...)`

## API Surface

Main methods:

- `EnqueueBackground(Action work)`
- `EnqueueBackground(Func<Task> work)`
- `PostToGameLoop(Action action)`
- `RunBackgroundAndPostResult<TResult>(Func<TResult> backgroundWork, Action<TResult> onGameLoop, Action<Exception>? onError = null)`
- `RunBackgroundAndPostResultAsync<TResult>(Func<Task<TResult>> backgroundWork, Action<TResult> onGameLoop, Action<Exception>? onError = null)`

## Events

For event-driven orchestration:

- `ExecuteBackgroundJobEvent`
- `ExecuteMainThreadEvent`

These can be published through the game event bus when you want decoupled producers/consumers around job execution.

## Example

```csharp
_backgroundJobService.RunBackgroundAndPostResult(
    () => BuildPathfindingChunk(chunkId),
    chunk =>
    {
        // Runs on game loop thread.
        _navigationService.AttachChunk(chunk);
    },
    ex =>
    {
        _logger.Error(ex, "Background chunk generation failed.");
    }
);
```

## Operational Notes

- Keep game-loop callbacks small and deterministic.
- Prefer batching work in background and applying one compact result on game loop.
- Route failures through `onError` and log with enough context to retry safely.
