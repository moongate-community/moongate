# Architecture overview

A one-page tour of how the server is put together. For class-level detail,
the [C# API reference](../api/index.md) is generated from the same code.

## Solution layout

Seven projects under `src/`, each with one job:

| Project | Owns |
|---|---|
| `Moongate.Core` | Primitives shared by everything: `Serial`, geometry (`Point3D`), core interfaces and extensions. |
| `Moongate.Network` | The UO wire protocol: packet framing, compression (huffman), incoming/outgoing packet types, handler plumbing and middlewares. |
| `Moongate.Persistence` | Snapshot persistence: persisted entities (accounts, mobiles, items), serial generators, and the seeder that creates the default `admin` account. |
| `Moongate.Scripting` | The Lua runtime integration: script loading/lifecycle and the built-in `log` and `game` modules. |
| `Moongate.Server` | The composition root: `Program.cs`, DI wiring, packet handlers, game services (items, mobiles, loot, accounts, characters), data loaders, embedded YAML assets, and the `item`/`mobile`/`loot` Lua modules. |
| `Moongate.Ultima` | UO client file access (art, maps, animations, tiles, localization). Deliberately standalone: it references **no** other Moongate project. |
| `Moongate.UO.Data` | UO domain data types: item/mobile/loot template DTOs, enums (`SkillName`, `LayerType`, …), hues. |

## One game loop

Moongate runs a **single game loop thread**. Everything that mutates world
state — event handlers, timers, spawns — happens on that thread:

- At startup the engine posts onto the loop, captures the loop-thread marker,
  and only then publishes `world_ready`; Lua event handlers are always
  dispatched loop-affine.
- Mutating Lua calls off the loop log a warning (`LoopGuard`); `game.post`
  and the `game.schedule*` timers are the way onto the loop from anywhere
  else. See [How scripting works](../scripting/guides/how-scripting-works.md).
- A job system with worker threads exists for off-loop work, and an event bus
  connects services without direct coupling.

## Networking

A single TCP listener (default `0.0.0.0:2593`) carries both login and game
traffic. Incoming bytes are framed and decompressed in `Moongate.Network`,
decoded into typed packets, and dispatched to handler classes collected in
DI as `IPacketHandlerRegistration` (login and character-selection handlers,
client version, general information, skill locks, and single/double click
today). The handlers are wired up in one place — `MoongatePacketHandlersPlugin`
— via the `RegisterPacketHandler<T>()` extension, keeping `Program.cs` free of
the opcode list. The
login→game-server handoff uses a one-time auth key with a 30-second TTL.
Targets ClassicUO 7.x only — the advertised feature flags are a constant,
modern set.

## Domain events

Handlers stay thin: rather than acting on the world themselves, they publish
**domain events** (`CharacterCreatedEvent`, `PlayerEnteredWorldEvent`, the
click events, …) on the event bus. Behaviour lives in subscribers — classes
implementing `IEventSubscriberRegistration`, registered with
`RegisterEventSubscriber<T>()` in `MoongateEventSubscribersPlugin` and attached
to the bus at startup by `EventSubscriberService`, the event-side twin of how
the network service wires packet handlers.

Publishing is synchronous — it returns once every subscriber has run — and
inbound packets are already marshalled onto the game loop, so subscribers run
loop-affine and may touch world state directly. Opening the paperdoll (0x88)
when you double-click a humanoid is the first of these, and mirrors ModernUO,
where the paperdoll is likewise a subscriber rather than packet-handler logic.

## Persistence

World state is persisted as **binary snapshots** per entity kind (accounts,
mobiles, items) under the root's `saves/` directory. Entity stores hand out
serials from per-kind generators; a seeder creates the default administrator
account on first run. There is no SQL database and no ORM — by design.

A `Serial` says what it identifies by where it falls: mobiles below
`0x40000000`, items above it — but only up to `Serial.MaxItem`. The rest of the
range is the **virtual band**, for things the client must be able to identify
that the server does not own as entities. Hair is the one we have: it is a
property of a mobile, yet every layer entry on the wire needs a serial, so
`IVirtualSerialService` allocates one from the band. Virtual serials are never
persisted — they are reissued on each boot.

## Data and plugins

At startup, loaders seed and then read the root's `data/` and `templates/`
YAML (world data, item/mobile/loot templates — see the
[data file reference](../scripting/data/item-templates.md)), validating
referential integrity where it matters (loot entries must point at real item
templates). Functionality is composed from **plugins** registered in
`Program.cs` (persistence, scripting, script modules, data loaders, packet
handlers, event subscribers); drop-in
assemblies in the root's `plugins/` directory are loaded from disk the same
way.
