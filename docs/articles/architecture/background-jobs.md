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

## Async Work Scheduler

For higher-level async features that need dedupe and keyed scheduling, Moongate also exposes:

- `IAsyncWorkSchedulerService.TrySchedule<TKey, TResult>(...)`

This sits above `IBackgroundJobService` and adds:

- keyed in-flight deduplication
- a named logical queue per feature
- background execution for the expensive part
- game-loop callback for the final apply step

Typical use cases:

- OpenAI-backed NPC dialogue
- slow external API calls
- expensive data preparation that must later mutate game state safely

Example:

```csharp
_asyncWorkSchedulerService.TrySchedule(
    "npc-dialogue",
    npc.Id,
    cancellationToken => _openAiNpcDialogueClient.GenerateAsync(request, cancellationToken),
    response =>
    {
        // Runs back on the game loop.
        ApplyDialogueResult(npc, response);
    },
    ex => _logger.Error(ex, "NPC dialogue generation failed for {NpcId}.", npc.Id),
    TimeSpan.FromSeconds(30)
);
```

Use `IBackgroundJobService` directly when you only need raw background execution.
Use `IAsyncWorkSchedulerService` when you need feature-level dedupe and safe result application.

## Lua Async Jobs

Lua scripts can also use the higher-level `async_job` module for named background jobs.

That path is built on top of `IAsyncWorkSchedulerService` and keeps the same contract:

- background work off the game loop
- completion callback back on the game loop
- no world mutation from the worker

See [Async Jobs](../scripting/async-jobs.md).

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
