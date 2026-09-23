# Game loop and timers

The game loop owns synchronous world mutations, synchronous packet handlers and timer
callbacks on one dedicated thread. Async packet handlers run off-loop and post any
state changes back through `PacketContext.RunOnGameLoopAsync`. Socket I/O and disk
writes belong outside that thread. Inject `IGameLoopService` and
`ITimerService` into host/plugin components;
their contracts are in `Moongate.Server.Core` and implementations in the
`Moongate.Server` executable project.

## Scheduling and limits

There is **no fixed game-loop tick rate**. The loop drains a bounded batch of
commands, processes due timers, then waits for new work or the next timer deadline
when idle. The timer wheel's default **8 ms resolution** rounds timer deadlines
upward; it is not a guarantee of one simulation update every 8 ms.

| Option object | Property | Default |
| --- | --- | --- |
| `GameLoopOptions` | `QueueCapacity` | 4096 queued items, excluding the active item |
| `GameLoopOptions` | `MaxWorkItemsPerBatch` | 256 attempted items |
| `GameLoopOptions` | `WorkItemBudget` | 5 ms per batch |
| `TimerWheelOptions` | `TickDuration` | 8 ms |
| `TimerWheelOptions` | `WheelSize` | 512 buckets |
| `TimerWheelOptions` | `MaxPendingTimers` | 65,536 registrations |
| `TimerWheelOptions` | `MaxCallbacksPerBatch` | 256 attempted callbacks |
| `TimerWheelOptions` | `CallbackBudget` | 5 ms per batch |

These positive values are C# options registered in `Program.cs`, not TOML fields.
Elapsed-time budgets are cooperative: a running item/callback is never interrupted.
An expensive handler can exceed the budget and delay every player and timer.
Repeating timers use fixed-rate deadlines, coalesce missed occurrences after a
pause and never run overlapping callbacks on the loop.

## Admission is different from completion

`TryPost(item)` returns false if the loop is full or not running; it never executes
inline. `PostAsync(item, token)` waits for queue space and completes on admission,
not execution. Await each producer call so pending producers do not grow without
bound. Cancellation before acceptance rejects the item; after acceptance it does
not retract it. Calls to `PostAsync` from the loop thread are rejected.

Carry a completion task when an external caller needs the result. This
`ReadValueWorkItem.cs` keeps continuations off the loop and leaves unexpected
exceptions fatal while also informing the caller:

```csharp
using Moongate.Server.Core.Interfaces.GameLoop;

public sealed class ReadValueWorkItem : IGameLoopWorkItem
{
    private readonly Func<int> _read;
    private readonly TaskCompletionSource<int> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<int> Completion => _completion.Task;

    public ReadValueWorkItem(Func<int> read)
    {
        _read = read;
    }

    public void Execute()
    {
        try
        {
            _completion.TrySetResult(_read());
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
            throw;
        }
    }
}
```

The following `Program.cs` is runnable in a .NET 10 console project referencing the
source `src/Moongate.Server/Moongate.Server.csproj`; the implementation is not a
NuGet package. Plugins use the already-started injected services instead of
constructing another loop:

```csharp
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;

var timers = new TimerWheelService(new TimerWheelOptions(), TimeProvider.System);
using var loop = new GameLoopService(new GameLoopOptions(), timers, TimeProvider.System);
await timers.StartAsync();
await loop.StartAsync();
try
{
    var item = new ReadValueWorkItem(() => loop.IsOnLoopThread ? 42 : -1);
    await loop.PostAsync(item);
    var winner = await Task.WhenAny(item.Completion, loop.Completion)
        .WaitAsync(TimeSpan.FromSeconds(5));
    if (winner == loop.Completion)
    {
        await loop.Completion; // Propagate the original fatal loop error, if any.
        throw new InvalidOperationException("Loop stopped before the result");
    }
    if (await item.Completion != 42)
    {
        throw new InvalidOperationException("Wrong execution thread");
    }

    var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    timers.RegisterTimer("once", TimeSpan.FromMilliseconds(16), () => fired.TrySetResult());
    await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));

    var repeated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var repeatId = timers.RegisterTimer("pulse", TimeSpan.FromMilliseconds(16),
        () => repeated.TrySetResult(), repeat: true);
    await repeated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    timers.UnregisterTimer(repeatId);
    Console.WriteLine("Loop completion and timer callbacks verified");
}
finally
{
    await loop.StopAsync();
    await timers.StopAsync();
}
await loop.Completion;
```

Do not `.Wait()`/`.Result` a task that requires this loop from inside a handler.
Use `IsOnLoopThread` when an API supports a direct synchronous owner-thread path.
Copy borrowed network data before posting it; see [Packets and handlers](packets.md).

## Timer lifecycle

`RegisterTimer(name, interval, callback, delay: ..., repeat: ...)` returns an opaque
string ID. `interval` and an optional first `delay` must be positive; without a
delay the first callback is due after the interval. Registration is allowed before
startup, but fails after shutdown or when registration capacity is exhausted.
Duplicate logical names are allowed.

Use `UnregisterTimer(id)` for one registration, `UnregisterTimersByName(name)` for
a component's named registrations or `UnregisterAllTimers()` when you own the
entire service. Cancellation prevents a not-yet-claimed callback; an already-running
callback can finish. Services should retain their IDs and unregister them when
stopping. Avoid `async void` callbacks: asynchronous work needs an explicitly
owned worker and completion/error handling.

## Failures, shutdown and observation

An exception escaping an ordinary `IGameLoopWorkItem.Execute` is fatal to the loop
and faults its stable `Completion` task. Accepted items behind that failure may
never run, which is why callers watch both their own result and loop completion.
Timer callback exceptions are isolated and counted; they do not fault the loop.
Treat expected domain errors inside the handler and deliberately report a result.

Normal `StopAsync()` closes admission and timers, drains accepted work and joins
the loop thread. It performs cleanup even after failure; inspect `Completion` for
the original error. Stop cannot forcibly interrupt a blocking handler and has no
forced timeout. The terminal overload `StopAsync(finalWorkItem)` adds one final
synchronous capture after draining and propagates capture/loop failure; it is used
by world saving. Do not call either stop operation from the loop thread.

`GetMetricsSnapshot()` on the loop and timers exposes queue pressure, timings,
rejections, lateness and failures. The default host includes both metric providers.
See [Diagnostics](diagnostics.md), [custom metric providers](metric-providers.md)
and [world saves](persistence.md) for integration.
