# Architecture

Moongate's runtime is assembled by the server executable from focused projects:

- `Moongate.Network` owns UO framing, packet parsing, and packet serialization.
- `Moongate.Server` composes services, owns connected-player sessions, dispatches packets, and implements gameplay-facing handlers.
- `Moongate.Core` defines the small game-loop abstraction shared across runtime features.
- `Moongate.Scripting` exposes selected runtime capabilities to Lua without making scripts depend directly on server services.
- `Moongate.Persistence` supplies the account and character stores used by server services.

The core runtime path is:

```text
Program bootstrap
  -> lifecycle services start
  -> TCP connection gets a PlayerSession and per-connection framer
  -> complete frames are copied and posted to the main game loop
  -> packet handler parses the frame and changes session/game state
  -> outgoing packet is serialized and handed to the connection
```

Read these pages in runtime order:

1. [Startup lifecycle](./startup-lifecycle.md)
2. [Networking](./networking.md)
3. [Login and sessions](./login-and-sessions.md)
4. [Game loop](./game-loop.md)

The pages describe only behavior visible in Moongate source and the external interfaces it calls. They do not infer how SquidStd implements its container, event loop, transport, timers, or event bus internally.

## Source map

### Runtime

- `src/Moongate.Server/Program.cs`
- `src/Moongate.Server/Services/Network/NetworkService.cs`
- `src/Moongate.Server/Data/Session/PlayerSession.cs`
- `src/Moongate.Core/Interfaces/IGameLoopContext.cs`
- `src/Moongate.Scripting/Modules/GameLoopModule.cs`

### Tests

- `tests/Moongate.Tests/Server/LoginFlowIntegrationTests.cs`
- `tests/Moongate.Tests/Scripting/GameLoopModuleTests.cs`
