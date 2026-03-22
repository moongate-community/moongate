# Architecture Overview

Moongate v2 is organized around a single game-loop thread with explicit queues between subsystems.

## Runtime Pipeline

1. `NetworkService` accepts TCP data via `MoongateTCPServer` / `MoongateTCPClient`.
2. Incoming bytes are framed and parsed into `IGameNetworkPacket` instances.
3. Parsed packets are published to `IMessageBusService` as `IncomingGamePacket`.
4. `GameLoopService` drains the message bus and dispatches packets through `IPacketDispatchService` to `IPacketListener`.
5. Listeners and handlers enqueue outbound packets to `IOutgoingPacketQueue`.
6. `GameLoopService` flushes outbound packets through `IOutboundPacketSender`.

## Main Building Blocks

- `Moongate.Server`
  - Bootstrap, game loop, packet listeners/handlers, session services, file loaders.
- `Moongate.Network`
  - TCP client/server primitives, span-based readers/writers, transport middleware.
- `Moongate.Network.Packets`
  - Incoming/outgoing packet models, packet registry, packet attributes and generated definitions.
- `Moongate.Persistence`
  - Snapshot + journal persistence unit-of-work and repositories.
- `Moongate.Server/Http` (inside `Moongate.Server`)
  - Embedded ASP.NET Core HTTP service (`/health`, `/metrics`, `/scalar`, `/openapi/*`).
  - Serves the frontend SPA on `/` when UI hosting is enabled (default).
- `Moongate.Server.Metrics`
  - Metrics providers, collection, snapshots, HTTP mapping.
- `Moongate.Scripting`
  - Lua engine and script module integration.
- `Moongate.UO.Data`
  - UO domain entities, enums, template models and shared data types.
- `Moongate.Core`
  - Shared types, geometry, buffers, compression, dice notation, JSON utilities, random, text encoding.
- `Moongate.Abstractions`
  - Base service contracts (`IMoongateService`), email abstractions, service priority types.
- `Moongate.Email`
  - Email pipeline: Scriban template rendering and SMTP/no-op senders.
- `Moongate.Generators`
  - 9 source generators for compile-time registration (packets, listeners, events, commands, metrics, scripts, file loaders, Lua user data, version metadata).

## Concurrency Model

- Network I/O happens on transport callbacks/threads.
- Game state mutation is centralized in game-loop flow.
- Cross-thread handoff is explicit:
  - inbound: `IMessageBusService`
  - outbound: `IOutgoingPacketQueue`
- `IGameEventBusService` is used for decoupled in-process event publication.

## Session Model

- `GameNetworkSession`
  - Transport state (client reference, pending bytes, protocol state, seed, compression/encryption flags).
- `GameSession`
  - Gameplay/protocol state (account/character ids, client version, movement state, runtime character).

## Design Notes

- Packet registration is attribute-driven (`[PacketHandler(...)]`) and materialized by `PacketTable.Register(...)`.
- Runtime packet processing uses `IGameNetworkPacket.TryParse(...)` and `Write(ref SpanWriter)`.
- Persistence currently stores world data in `save/world.snapshot.bin` + `save/world.journal.bin`.
- `UOMobileEntity` intentionally does not include RPG-style `Level`/`Experience`; the base mobile model follows UO/ModernUO semantics.
- Region subsystem is intentionally based on the ModernUO approach (selected as the strongest reference implementation): polymorphic JSON (`$type`) plus map/sector indexing.
- Region selection for a location uses deterministic ordering: `Priority` descending, then parent-child depth (`ChildLevel`) for ties.
- Large gameplay subsystems should be modeled as `Features`, not generic `Engines`. See [Features vs Services](features-vs-services.md).

---

**Previous**: [Solution Structure](solution.md) | **Next**: [Features vs Services](features-vs-services.md)
