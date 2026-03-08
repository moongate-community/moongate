# Bootstrap System

Moongate v2 uses a phased bootstrap system to initialize the server in a deterministic order.

## Overview

`MoongateBootstrap` is the composition root. It executes bootstrap phases in priority order, each responsible for a specific initialization concern.

## Phases

### Phase 1: InfrastructurePhase (Order: 1)

Sets up foundational infrastructure:

- Checks and creates required directories
- Initializes Serilog logger with console and file sinks
- Loads `moongate.json` configuration merged with environment variables and CLI arguments
- Validates the UO client data directory path
- Copies bundled data assets to the working directory

### Phase 2: ServiceRegistrationPhase (Order: 2)

Registers all services in the DryIoc container:

- Registers HTTP server (if enabled in configuration)
- Registers script module definitions and Lua user data types
- Calls `BootstrapServiceRegistration` to register all game services
- Registers console commands (via source-generated `BootstrapConsoleCommandRegistration`)
- Registers game event listeners (via source-generated `BootstrapGameEventListenerRegistration`)

### Phase 3: WiringPhase (Order: 3)

Wires runtime registrations after all services are available:

- Registers file loaders (via source-generated `BootstrapFileLoaderRegistration`)
- Registers packet handlers (via source-generated `BootstrapPacketHandlerRegistration`)
- Subscribes game event listeners to the event bus
- Registers command executors with the command system

## Service Startup

After all phases complete, `MoongateBootstrap` starts services ordered by `ServicePriority`:

1. Services implementing `IMoongateService` are started via `StartAsync`.
2. File loaders run, populating data services.
3. The game loop begins processing.

## Design Notes

- Each phase is a separate class implementing `IBootstrapPhase` with an `Order` property.
- Phases are discovered and executed in ascending order.
- Source generators remove the need for runtime reflection scanning during registration.
- The phased approach ensures dependencies are available before consumers register.

---

**Previous**: [Solution Structure](solution.md) | **Next**: [Source Generators](generators.md)
