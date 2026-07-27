# Hosted services

A **hosted service** is a plugin component with a start/stop lifecycle. The HTTP API and the
admin console are both hosted services; so is the greeter from
[your first plugin](first-plugin.md). Use one whenever you need to own a resource — a socket,
a timer, a connection — for the lifetime of the server.

## The lifecycle

```csharp
public interface ISquidStdService
{
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
```

`StartAsync` runs once at server startup, `StopAsync` once at shutdown. Register the service
in your plugin's `Configure`:

```csharp
container.RegisterStdService<HeartbeatService, HeartbeatService>();
```

## Own your resources

Start your background work in `StartAsync` and tear it down in `StopAsync`. `StartAsync`
should not block — kick off the loop and return. Keep a handle to the loop so `StopAsync` can
await it, and honour the cancellation token so shutdown is prompt.

```csharp
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;

namespace MyShard.Heartbeat.Services;

public sealed class HeartbeatService : ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<HeartbeatService>();
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMinutes(5));
    private Task? _loop;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _loop = RunAsync(cancellationToken);

        return ValueTask.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {
                _logger.Information("Shard heartbeat");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _timer.Dispose();

        if (_loop is not null)
        {
            await _loop;
        }
    }
}
```

## Stay optional

An optional plugin must never take the server down. If your `StartAsync` does something that
can fail at runtime — binding a port, opening a file — catch it, log it, and return normally.
The admin console does exactly this: a failed bind is logged and swallowed, so a misconfigured
console leaves the game running. See [the admin console](../../under-the-hood/admin-console.md).

> An exception that escapes `Configure` or `StartAsync` aborts startup. That is the right
> behaviour for a *required* plugin and the wrong behaviour for an optional one — decide which
> yours is and handle failures accordingly.

## Touching game state

`StartAsync` and your background loop do **not** run on the game loop. To read or mutate world
state from a hosted service, marshal onto the loop through `IMainThreadDispatcher`
(`SquidStd.Core.Interfaces.Threading`) — inject it like any other dependency. If your work is
naturally a *reaction* to something happening in the world, an
[event subscriber](event-subscribers.md) is usually the better fit: its handlers already run
on the loop.

## Disposing

Implement `IDisposable` when your service owns something that must be released beyond what
`StopAsync` handles. Per the code conventions, `Dispose` is the **last** member of the class.
