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
- Contracts and data types for sessions, the game loop, timers, world saves, and diagnostics.

## Example

Register the event bus and subscribe to a lifecycle event. This example publishes the event manually to demonstrate the bus; it does not start a Moongate server.

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

## Dependencies and scope

This package depends on `Moongate.Core`, `Moongate.Network`, and `Moongate.Network.Packets`. DryIoc is available through the dependency graph.

The executable host and implementations of server runtime services are provided by `Moongate.Server`, which is not distributed as part of this library package. Referencing this package does not start the host, listener, timers, or game loop.

## License and source

Licensed under AGPL-3.0-or-later. See the [source repository and license](https://github.com/moongate-community/moongate).
