# Architecture overview

Moongate is deliberately small, because it is **two layers**:

1. **[SquidStd](https://www.nuget.org/packages?q=SquidStd)** — a set of NuGet
   packages that provide the generic server infrastructure: host bootstrap,
   YAML config, dependency injection, plugins, the game loop, an event bus, a
   job system, network buffers, binary persistence and the Lua runtime.
2. **Moongate** — everything Ultima Online: the wire protocol, game services,
   world state, YAML templates and client-file access.

Rule of thumb when reading the code: *if it could exist in any game server, it
comes from SquidStd; if it smells of Britannia, it's Moongate.*

## The SquidStd toolkit

| Package | What it provides | Who uses it |
|---|---|---|
| `SquidStd.Core` | Primitives, `SquidStdConfig` (YAML config), the `IEventBus` contract, embedded-resource and path utilities. | `Moongate.Core`, `Moongate.Server` |
| `SquidStd.Services.Core` | The runtime: `SquidStdBootstrap` (the host), the **event loop** (game loop), main-thread dispatcher, timer wheel, job system and the event bus implementation. | `Moongate.Server` |
| `SquidStd.Abstractions` | Registration seams: `RegisterConfigSection<T>`, `RegisterStdService<TInterface, TService>`. | `Moongate.Server` |
| `SquidStd.Network` | `SpanReader`/`SpanWriter` (allocation-free packet IO) and TCP server plumbing. | `Moongate.Network` |
| `SquidStd.Persistence` + `.MessagePack` | Snapshot store with MessagePack binary serialization. | `Moongate.Persistence` |
| `SquidStd.Plugin` + `.Abstractions` | The plugin contract and loading (built-in classes or drop-in assemblies from a directory). | all plugin projects |
| `SquidStd.Scripting.Lua` | The Lua runtime, module registration and value marshalling. | `Moongate.Scripting` |

DI is [DryIoc](https://github.com/dadhi/DryIoc); the CLI entry point is
[ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework); logging
is Serilog, configured by the bootstrap.

## How the server starts

`Program.cs` in `Moongate.Server` is the whole composition root, and it reads
top to bottom:

1. **CLI / environment.** `--root-directory` (or `MOONGATE_ROOT`, default
   `./moongate_root`) picks the runtime root; `--uo-directory` (default
   `~/uo`) points at the UO client files.
2. **Config first.** `SquidStdConfig.Load("moongate", root)` reads
   `moongate.yaml` eagerly, *before* the container exists. CLI overrides are
   applied to the config object right away, and startup fails fast if
   `UltimaDirectory` is not set.
3. **Bootstrap.** `SquidStdBootstrap.Create(...)` builds the host;
   `ConfigureLogging()` brings Serilog up so even plugin loading is visible.
4. **Plugins.** `UsePlugins(...)` registers drop-in assemblies from the root's
   `plugins/` directory plus the seven built-in Moongate plugins: persistence,
   scripting, script modules, data loaders, packet handlers, event
   subscribers and HTTP. The last one is genuinely optional —
   start with `--disable-web-plugin` and the server runs with no web stack at
   all. See [REST API](rest-api.md).
5. **Services.** `ConfigureServices(...)` registers the game services
   (accounts, characters, items, mobiles, loot, world, sessions, network) and
   the SquidStd runtime services: main-thread dispatcher, timer wheel, event
   loop, job system and event bus.
6. **Run.** `RunAsync` starts the host. UO client files are loaded
   (`FilesLoadedEvent`), the engine comes up (`EngineStartedEvent`), default
   timers start, and the engine posts onto the game loop to capture the
   loop-thread marker before publishing `WorldReadyEvent`.

## One game loop

The SquidStd **event loop** is Moongate's single game-loop thread. Everything
that mutates world state — packet handlers, event subscribers, timers, Lua —
runs on it:

- The loop-thread marker captured at startup backs `LoopGuard`: mutating Lua
  calls from any other thread log a warning. `game.post` and the
  `game.schedule*` timers are the way onto the loop from anywhere else. See
  [How scripting works](../scripting/guides/how-scripting-works.md).
- The loop is configured for a busy server: 1 ms idle sleep and a 250 ms
  slow-tick warning threshold.
- CPU-bound or blocking work goes to the SquidStd **job system**
  (`ProcessorCount − 1` workers) instead of the loop.

## Networking

A single TCP listener (default `0.0.0.0:2593`) carries both login and game
traffic. `Moongate.Network` frames and decompresses (huffman) the byte stream
with SquidStd's `SpanReader`/`SpanWriter`, decodes it into typed packet
records, and dispatches them to handler classes collected in DI as
`IPacketHandlerRegistration` — all wired in one place,
`MoongatePacketHandlersPlugin`, via `RegisterPacketHandler<T>()`. The
login→game-server handoff uses a one-time auth key with a 30-second TTL.
Moongate targets ClassicUO 7.x only, so the advertised feature flags are a
constant, modern set.

Every implemented packet is documented in the
[packet reference](../packets/index.md), generated straight from the packet
classes.

## Domain events

Packet handlers stay thin: rather than acting on the world themselves, they
publish **domain events** (`CharacterCreatedEvent`, `PlayerEnteredWorldEvent`,
the click events, …) on the SquidStd event bus. Behaviour lives in
subscribers — classes implementing `IEventSubscriberRegistration`, registered
with `RegisterEventSubscriber<T>()` in `MoongateEventSubscribersPlugin` — the
event-side twin of how packet handlers are wired.

Publishing is synchronous and inbound packets are already on the game loop, so
subscribers run loop-affine and may touch world state directly. Two
subscribers ship today, and both mirror ModernUO, where the same behaviour
likewise lives outside the packet handlers: double-clicking a humanoid opens
its paperdoll (`0x88`), and double-clicking a container opens its gump
(`0x24`) and fills it (`0x3C`).

Whether an item is a container is the template's answer, not the item's:
`ItemEntity` carries the `TemplateId` it was built from and nothing else on the
subject, so the question is asked of the template's `Container:` block every
time. ModernUO asks the same question of its class hierarchy — `Container` is a
base class there — which an entity built from data does not have; the template
id is the seam that replaces it.

The gump follows the same chain ModernUO walks: the template's own `GumpId`
wins, failing that the gump table is asked for one matching the graphic
(ModernUO's `ContainerData.GetData(itemID)` over `containers.cfg`, which is
where our table comes from), failing that it is the plain bag. That last step
is why the backpack appears in neither: it lands on the default.

The client's `tiledata.mul` also flags container graphics, and we deliberately
ignore it — as ModernUO does. It answers a different question ("is this graphic
a container?" rather than "does this shard open it?"), and the two disagree
often enough to matter: key rings, potion kegs and spellbooks are all flagged
graphics that no shard opens.

## Persistence

World state is saved as **binary snapshots** per entity kind (accounts,
mobiles, items) under the root's `saves/` directory, serialized with
MessagePack through `SquidStd.Persistence`. Entity stores hand out serials
from per-kind generators; a seeder creates the default `admin` account on
first run. There is no SQL database and no ORM — by design.

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
templates). Beyond the seven built-in plugins, drop-in assemblies in the root's
`plugins/` directory are loaded from disk the same way — and
`Moongate.Server.Abstractions` is the assembly such a plugin references to
consume the game's services and events without ever seeing the server.

## Scripting

`SquidStd.Scripting.Lua` hosts the Lua runtime; `Moongate.Scripting` owns
script loading and the built-in `log` and `game` modules, while the
game-facing modules (`item`, `mobile`, `loot`, `events`) live in
`Moongate.Server` and are registered by `MoongateScriptModulesPlugin`. Event
handlers registered from Lua are always dispatched on the game loop.

## Solution layout

Nine projects under `src/`, each with one job:

| Project | Owns | SquidStd packages |
|---|---|---|
| `Moongate.Core` | Primitives shared by everything: `Serial`, geometry (`Point3D`), core interfaces and extensions. | Core |
| `Moongate.Http.Plugin` | The whole REST surface: ASP.NET plumbing, JWT, OpenAPI/Scalar and every endpoint group — the game-facing groups consume `Moongate.Server.Abstractions`. | Abstractions, Plugin.Abstractions |
| `Moongate.Network` | The UO wire protocol: framing, huffman compression, packet types, handler plumbing and middlewares. | Network, Plugin.Abstractions |
| `Moongate.Persistence` | Snapshot persistence, serial generators, the `admin` seeder. | Persistence, Persistence.MessagePack, Plugin.Abstractions |
| `Moongate.Scripting` | Lua runtime integration: script lifecycle, `log` and `game` modules. | Scripting.Lua, Plugin.Abstractions |
| `Moongate.Server` | The composition root: `Program.cs`, DI wiring, packet handlers, game services, data loaders, embedded YAML assets, game-facing Lua modules. | Services.Core, Core, Abstractions, Plugin |
| `Moongate.Server.Abstractions` | The game's contract assembly: every service interface (accounts, items, mobiles, world, sessions, network), the domain events, the session model and the yaml-bound config records. What a plugin references instead of the server. | — |
| `Moongate.Ultima` | UO client file access (art, maps, animations, tiles, localization). Deliberately standalone: it references **no** other Moongate project. | — |
| `Moongate.UO.Data` | UO domain data types: template DTOs, enums (`SkillName`, `LayerType`, …), hues. | — |

For class-level detail, the [C# API reference](../api/index.md) is generated
from the same code.
