![Moongate](https://raw.githubusercontent.com/moongate-community/moongate/develop/images/moongate_logo.png)

# Moongate.Server.Core

Plugin-facing server contracts, events, registries, and dependency injection extensions for Moongate.

## Installation

Requires .NET 10. Use the package version available in your configured NuGet feed.

```shell
dotnet add package Moongate.Server.Core
```

## Features

- Plugin contracts, metadata, and registration APIs.
- Server lifecycle events and an event bus integrated with DryIoc.
- Registrations for server services, packet handlers, and commands.
- Contracts and data types for transport connections, sessions, the game loop, timers, world saves, and diagnostics.

## Example

Register the event bus and subscribe to a lifecycle event. This example publishes the event manually to demonstrate the bus;
it does not start a Moongate server.

<!-- nuget-smoke:Program.cs -->

```csharp
using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;

using var container = new Container();
container.RegisterMoongateEventBus();
var bus = container.Resolve<IMoongateEventBus>();

using var subscription = bus.Subscribe<MoongateStartedEvent>((_, cancellationToken) =>
{
    Console.WriteLine("Started");
    return Task.CompletedTask;
});

await bus.PublishAsync(new MoongateStartedEvent());
```

`SubscribeAll` registers a handler invoked for every published event, independent of
type — dispatched after that event's own typed subscribers, in subscription order.
Each catch-all subscription is independent and returns its own disposable token,
exactly like `Subscribe<TEvent>`; disposing one never affects another.

## Dependencies and scope

This package depends on `Moongate.Core`, `Moongate.Api`, `Moongate.Network`, and `Moongate.Network.Packets`. DryIoc is
available through the dependency graph.

The executable host and implementations of server runtime services are provided by `Moongate.Server`, which is not
distributed as part of this library package. Referencing this package does not start the host, listener, timers, or game
loop.

## Connections and game coordination

`IConnectionService` tracks transport connections independently of game sessions. Its
`TryGet` returns only live connections whose admission is still open. `DisconnectAsync`
closes admission immediately and joins both the close request and actual transport
completion. Closing connections remain in `Count` and membership snapshots until
cleanup finishes. Stop is terminal and reports cleanup failures. The three-argument
`TryGet` overload also captures an owner-requested disconnect signal atomically with the
connection; the sender uses that stable signal to classify intentionally interrupted writes.
Remote closure or a send failure that closes its own transport does not complete it.

`INetworkService` owns listeners and raises synchronous `ConnectionAccepted`,
`DataReceived`, and `ConnectionClosed` events. **Receive memory is borrowed until the
callback returns:** decode or copy it before posting work elsewhere. A close notification
precedes full transport cleanup; never synchronously wait for that cleanup in the callback.
`NetworkListenerOptions` supplies endpoints and an optional per-connection pipeline factory.

`IGameServerService` coordinates game sessions and packet dispatch above that boundary.
`IPacketSendService` sends through the connection registry without requiring a game session.
The executable host provides these implementations and their startup order.

### Compatibility

Since 0.2.0, sessions hold an `INetworkConnection` instead of a `MoongateTcpClient`;
consumers and plugins built against 0.1.x must be rebuilt. See
[Transport and game ownership](https://moongate.sh/server/network-game-separation/)
for the composition sample, the shutdown sequence and the upgrade notes.

## Runtime guides

See [packets and handlers](https://moongate.sh/server/packets/),
[game loop and timers](https://moongate.sh/server/game-loop-and-timers/)
and [world saves](https://moongate.sh/server/persistence/)
for registration, threading, completion and shutdown examples.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
