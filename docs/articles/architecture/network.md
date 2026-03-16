# Network System

This page describes the current network stack used by Moongate v2.

## Core Components

- `MoongateTCPServer` (`Moongate.Network`)
  - Listens on endpoints and raises connect/disconnect/data events.
- `MoongateTCPClient` (`Moongate.Network`)
  - Per-connection transport abstraction with send/receive and middleware support.
- `NetworkService` (`Moongate.Server.Services.Network`)
  - Owns protocol framing/parsing and session lifecycle.
- `GameNetworkSessionService`
  - Stores active `GameSession` instances keyed by session id.
- `PacketRegistry` / `PacketTable`
  - Resolve opcode metadata and packet factories.

## Inbound Flow

1. `OnDataReceived` receives bytes from `MoongateTCPClient`.
2. Bytes are appended to per-session pending buffer (`GameNetworkSession.WithPendingBytesLock(...)`).
3. `NetworkService` resolves packet length:
   - fixed length from `PacketDescriptor.Length`
   - variable length from bytes `[1..2]` (big-endian, includes header)
4. Raw packet bytes are parsed with `packet.TryParse(rawPacket)`.
5. Parsed packet is published to `IMessageBusService`.

Protection currently implemented:

- max pending buffer size
- max declared packet length
- unknown opcode handling with protocol-violation counter
- disconnect after repeated violations

## Outbound Flow

1. Handlers/listeners enqueue packets into `IOutgoingPacketQueue`.
2. `GameLoopService` drains queue each loop iteration.
3. `IOutboundPacketSender` serializes packet with `SpanWriter` and sends through `MoongateTCPClient.SendAsync(...)`.
4. If packet logging is enabled, hex dump is written with `PacketData` context.

Current gameplay-facing outbound examples:

- `PlayerStatusHandler` emits `PlayerStatusPacket` (`0x11`) for basic status requests
- `PlayerStatusHandler` emits `SkillListPacket` (`0x3A`) for skill window requests
- `ChatSystemService` emits `ChatCommandPacket` (`0xB2`) for classic conference chat UI, channel membership, PMs, moderator/voice changes, and system responses
- item packet traffic is now split behind `ItemHandler` into focused services:
  - `ItemBookService` handles classic book traffic (`0xD4`, `0x66`)
  - `ItemInteractionService` handles single-click / double-click item interaction flows
  - `ItemManipulationService` handles pickup, drop, equip, and wear refresh flows

## Compression and Middleware

`GameNetworkSession` can toggle transport middleware:

- `EnableCompression()` / `DisableCompression()`
- `EnableEncryption()` / `DisableEncryption()`

Compression middleware is attached/removed on `MoongateTCPClient` pipeline.

## Session Lifecycle

- connect: create or refresh `GameSession` + `GameNetworkSession`
- disconnect: remove session from `GameNetworkSessionService`
- protocol states: `AwaitingSeed`, `Authenticated`, `InGame`, etc.

## Current Request Flows

Important current request/response pairs:

- `0x34 Get Player Status`
  - `BasicStatus` -> `0x11 Player Status`
  - `RequestSkills` -> `0x3A Skill List`
- `0xB5 Open Chat Window`
  - client open request -> server creates/refreshes runtime chat user and returns `0xB2` chat-window command
- `0xB3 Chat Text`
  - action dispatch in `IChatSystemService`
  - supports conference message/join/create/rename/password, PM, ignore, ops/voice, whois, kick, emote
- `0x66 Book Pages`
  - `ItemHandler` routes to `ItemBookService`
  - page request -> server book page response
  - writable page save -> server persists updated page content
- `0xD4 Book Header New`
  - `ItemHandler` routes to `ItemBookService`
  - writable header save -> server persists `title` / `author`

## Useful Runtime Diagnostics

- Registered packets are logged at startup (`NetworkService.ShowRegisteredPackets()`).
- Unknown opcode warnings include descriptor description when available.
- Metrics exposed via `INetworkMetricsSource`.

---

**Previous**: [Architecture Overview](overview.md) | **Next**: [Game Loop](game-loop.md)
