# Event subscribers

An **event subscriber** reacts to something that happens in the world — a player double-clicks,
a container opens, the world finishes loading. When your plugin's job is a *reaction* rather
than owning a resource, a subscriber is the natural fit: its handlers already run on the game
loop, so they can touch world state directly.

## The interface

```csharp
public interface IEventSubscriberRegistration
{
    void Subscribe(IEventBus eventBus);
}
```

In `Subscribe`, attach a handler for each event you care about. A handler is
`Task Handler(TEvent message, CancellationToken cancellationToken)`.

```csharp
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Events;
using Serilog;
using SquidStd.Core.Interfaces.Events;

namespace MyShard.Live.Subscribers;

/// <summary>Logs a banner once the world has finished loading.</summary>
public sealed class WorldReadySubscriber : IEventSubscriberRegistration
{
    private readonly ILogger _logger = Log.ForContext<WorldReadySubscriber>();

    public void Subscribe(IEventBus eventBus)
        => eventBus.Subscribe<WorldReadyEvent>(OnWorldReady);

    private Task OnWorldReady(WorldReadyEvent message, CancellationToken cancellationToken)
    {
        _logger.Information("=== MyShard is open for business ===");

        return Task.CompletedTask;
    }
}
```

`WorldReadyEvent` lives in `Moongate.Server.Abstractions.Data.Events`, alongside the other
domain events you can subscribe to.

## Registering

Register the subscriber in your plugin's `Configure`:

```csharp
container.RegisterEventSubscriber<WorldReadySubscriber>();
```

The event-subscriber service resolves every registration and attaches it to the bus at startup.

## Loop affinity

Domain-event handlers run on the game loop, so it is safe to read and mutate world state
directly inside them — no `IMainThreadDispatcher` needed. That is the difference from a
[hosted service](hosted-service.md), whose background loop runs off the game thread. A
subscriber can still inject any service it needs through its constructor.
