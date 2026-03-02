# Moongate v2

<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="240" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-.NET%2010-blueviolet" alt=".NET 10">
  <img src="https://img.shields.io/badge/AOT-enabled-green" alt="AOT Enabled">
  <img src="https://img.shields.io/badge/scripting-Lua-yellow" alt="Lua Scripting">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="GPL-3.0 License">
  <img src="https://img.shields.io/badge/status-development-orange" alt="Development Status">
</p>

[![CI](https://github.com/moongate-community/moongatev2/actions/workflows/ci.yml/badge.svg)](https://github.com/moongate-community/moongatev2/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongatev2/gh-pages/badges/tests.json)](https://github.com/moongate-community/moongatev2/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongatev2/gh-pages/badges/coverage.json)](https://github.com/moongate-community/moongatev2/actions/workflows/coverage.yml)
[![Code Quality](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongatev2/gh-pages/badges/quality.json)](https://github.com/moongate-community/moongatev2/actions/workflows/quality.yml)
[![Quality Gate](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongatev2/gh-pages/badges/quality-gate.json)](https://github.com/moongate-community/moongatev2/actions/workflows/quality.yml)
[![Security](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongatev2/gh-pages/badges/security.json)](https://github.com/moongate-community/moongatev2/actions/workflows/security.yml)
[![Latest Release](https://img.shields.io/github/v/release/moongate-community/moongatev2)](https://github.com/moongate-community/moongatev2/releases)
[![Latest Pre-release](https://img.shields.io/github/v/release/moongate-community/moongatev2?include_prereleases&label=pre-release)](https://github.com/moongate-community/moongatev2/releases)
[![Docs](https://github.com/moongate-community/moongatev2/actions/workflows/docs.yml/badge.svg)](https://github.com/moongate-community/moongatev2/actions/workflows/docs.yml)
[![Release](https://github.com/moongate-community/moongatev2/actions/workflows/release.yml/badge.svg)](https://github.com/moongate-community/moongatev2/actions/workflows/release.yml)
[![Docker Image](https://img.shields.io/docker/v/tgiachi/moongate?sort=semver)](https://hub.docker.com/r/tgiachi/moongate)
[![Docker Pulls](https://img.shields.io/docker/pulls/tgiachi/moongate)](https://hub.docker.com/r/tgiachi/moongate)
[![Docker Image Size](https://img.shields.io/docker/image-size/tgiachi/moongate/latest)](https://hub.docker.com/r/tgiachi/moongate)

Moongate v2 is a modern Ultima Online server project built with .NET 10.
It targets a clean, modular architecture with strong packet tooling, deterministic game-loop processing, and practical test coverage.

> Moongate is not a clone of ModernUO, RunUO, ServUO or any other emulator, and it does not aim to be. In fact, we owe a great deal of inspiration to these projects. Their legacy and technical achievements are invaluable, and this project would not exist without them. Thank you.

## Acknowledgements

Special thanks to the teams and contributors behind these projects, which strongly inspired Moongate:

- POLServer: <https://github.com/polserver/polserver>
- ModernUO: <https://github.com/modernuo/modernuo>

## Index

- [Project Goals](#project-goals)
- [Project Story](#project-story)
- [Frontend Preview](#frontend-preview)
- [Current Status](#current-status)
- [Spatial Chunk Strategy](#spatial-chunk-strategy)
- [World Generation Pipeline](#world-generation-pipeline)
- [UO Feature Support (Current)](#uo-feature-support-current)
- [Persistence](#persistence)
- [Templates](#templates)
- [Solution Structure](#solution-structure)
- [Source Generators (AOT)](#source-generators-aot)
- [Event And Packet Separation](#event-and-packet-separation)
- [Game Loop Scheduling](#game-loop-scheduling)
- [Requirements](#requirements)
- [Server Startup Tutorial](#server-startup-tutorial)
- [Quick Start](#quick-start)
- [Command System](#command-system)
- [Scripting](#scripting)
- [Item ScriptId Dispatch](#item-scriptid-dispatch)
- [Scripts](#scripts)
- [Benchmarks](#benchmarks)
- [Docker](#docker)
- [Docker Monitoring Stack](#docker-monitoring-stack)
- [Documentation](#documentation)
- [Development Notes](#development-notes)
- [Contributing](#contributing)
- [License](#license)

## Project Goals

- Build a maintainable UO server foundation focused on correctness and iteration speed.
- Keep networking and game-loop boundaries explicit and thread-safe.
- Model protocol packets with typed definitions and source-generated registration.
- Stay AOT-aware while preserving a smooth local development workflow.

## Project Story

You can read the background and motivation behind Moongate v2 here:

- <https://orivega.io/moongate-v2-rewriting-a-ultima-online-server-from-scratch-because-i-wanted-to/>

## Frontend Preview

I hate building frontend myself, so thanks to Codex I started adding a UI layer in `ui/`.

![UI Screen 1](images/ui/ui_screen1.png)
![UI Screen 2](images/ui/ui_screen2.png)

## Current Status

The project is actively in development and already includes:

- TCP server startup and connection lifecycle handling.
- Packet framing/parsing for fixed and variable packet sizes.
- Attribute-based packet mapping (`[PacketHandler(...)]`) with source generation.
- Inbound message bus (`IMessageBusService`) for network thread -> game-loop crossing.
- Domain event bus (`IGameEventBusService`) with initial events (`PlayerConnectedEvent`, `PlayerDisconnectedEvent`).
- Outbound event listener abstraction (`IOutboundEventListener<TEvent>`) for domain-event -> network side effects.
- Session split between transport (`GameNetworkSession`) and gameplay/protocol context (`GameSession`).
- Unit tests for core server behaviors and packet infrastructure.
- Lua scripting runtime with module/function binding and `.luarc` generation support.
- Lua metadata files (`definitions.lua`, `.luarc.json`) generated in configured `LuaEngineConfig.LuarcDirectory` during engine startup.
- Embedded HTTP host (`Moongate.Server/Http`) for health/admin endpoints and OpenAPI/Scalar docs.
- Dedicated HTTP rolling logs in the shared logs directory (`moongate_http-*.log`).
- Snapshot+journal persistence module (`Moongate.Persistence`) integrated in server lifecycle.
- ID-based persistence references for character equipment/container ownership.
- Interactive console UI with fixed prompt (`moongate>`) and Spectre-based colored log rendering.
- Timer wheel runtime metrics integrated in the metrics pipeline (`timer.*`).
- Timestamp-driven game loop scheduling with timer delta updates and optional idle CPU throttling.
- Region system adopted from ModernUO (chosen as the most robust baseline), including polymorphic JSON loading via `$type`.
- Spatial region resolution indexed by sector with deterministic ordering:
  - higher `Priority` first
  - then deeper parent/child hierarchy (`ChildLevel`) when priority ties.
- Region music mapped as typed `MusicName` and resolved by `MapId` + position.

## Spatial Chunk Strategy

Moongate uses a sector/chunk-based world streaming strategy instead of a pure range-view scan model.

- World data is indexed by sectors (`16x16`) and loaded lazily.
- When a sector is touched, Moongate loads entities (items + mobiles) around it in a configurable sector radius.
- Around player login and sector changes, snapshots are sent using sector radius windows.
- Sectors are created, populated, and reused in memory; inactive areas stay unloaded until requested.

Why this choice:

- Predictable memory growth and lower steady-state CPU usage on large worlds.
- Better cache locality for entity queries and network snapshot generation.
- Simpler scalability path for high-concurrency shards.

Compared to classic emulator approaches that rely mainly on repeated range-view scans, this model is intentionally closer to chunk-streaming systems (Minecraft-style): load/unload by sector boundaries with configurable warmup and sync radii.

For a detailed internal status snapshot, see `docs/plans/status-2026-02-19.md`.

## World Generation Pipeline

Moongate uses a world-generation pipeline based on `IWorldGenerator`.

- Each generator is a named unit (`Name`), orchestrated by `IWorldGeneratorBuilderService`.
- The builder supports:
  - full execution (`GenerateAsync()`),
  - targeted execution by name (`GenerateAsync("doors")`),
  - optional progress callback (`Action<string>`) for logs/progress output.
- Door generation is implemented as `DoorGeneratorBuilder` (`Name = "doors"`), with hardcoded scan regions (ModernUO-style) and `CanFit` filtering before accepting candidate placements.
- Current output is a generated placement record list (`MapId`, `Location`, `Facing`) used for debug/integration; item instantiation is intentionally decoupled.

Manual trigger:

- Command: `.spawn_doors`
- Scope: console + in-game admin command
- Behavior: runs only the `doors` generator and streams progress lines to command output.

## UO Feature Support (Current)

This section reflects the current server-side implementation status.

### Supported now

- Active inbound packet handlers:
  - Login/auth: `0xEF`, `0x80`, `0xA0`, `0x91`, `0x5D`, `0xBD`
  - Character: `0x00`
  - Movement: `0x02`, `0xC8`
  - Item interaction: `0x07`, `0x08`, `0x09`, `0x13`, `0x06`
  - Speech/chat: `0xAD`, `0xB5`
  - Targeting: `0x6C`
  - General info multiplexer: `0xBF`
  - Player status: `0x34`
  - Ping: `0x73`
  - Tooltip: `0xD6`
- `0xBF` subcommands currently wired in runtime:
  - `0x06` Party System
  - `0x1A` Stat Lock Change
  - `0x2C` Use Targeted Item
  - `0x2D` Cast Targeted Spell
  - `0x2E` Use Targeted Skill
- Active outbound gameplay packets include:
  - Login/session: `0x8C`, `0xA8`, `0xA9`, `0x1B`, `0x55`, `0x82`, `0xB9`
  - World/entity sync: `0x78`, `0x20`, `0x2E`, `0x24`, `0x3C`, `0x11`, `0x88`, `0xF3`, `0x23`
  - Movement/time: `0x22`, `0x21`, `0x5B`, `0xF2`
  - Environment/effects: `0xBC`, `0x4F`, `0x4E`, `0x6D`, `0x65`, `0x54`, `0x70`, `0xC0`, `0xC7`
  - UI/speech: `0xAE`, `0xB0`, `0xDD`

### Partially implemented

- Protocol model coverage is broader than runtime gameplay wiring:
  - many packet contracts exist in `Moongate.Network.Packets`,
  - only the opcodes listed above are currently connected to live handlers/flows.
- Item pipeline is functional for pickup/drop/equip/container refresh, but advanced cases (full trade/vendor/economy semantics) are still expanding.
- Lua runtime is integrated (commands, speech, targeting, gump builder), but high-level game systems are still script-surface growth areas.

### Not yet implemented (major areas)

- Full combat loop (swing/spell damage pipeline, notoriety-driven combat rules).
- Skill system execution and progression.
- NPC AI, vendors, loot systems, pathfinding, spawn regions.
- World simulation breadth (housing, boats, advanced map interactions, seasons/weather effects gameplay-side).
- Economy systems and complete trading/vendor behavior.
- Full UO protocol listener coverage (many opcodes intentionally unhandled yet).

## Persistence

Moongate uses a lightweight file-based persistence model implemented in `src/Moongate.Persistence`:

- Snapshot file (`world.snapshot.bin`) for full world state checkpoints.
- Append-only journal (`world.journal.bin`) for incremental operations between snapshots.
- MemoryPack binary serialization for compact and fast read/write.
- Per-operation checksums in journal entries to detect truncated/corrupted tails.
- Thread-safe repositories for accounts, mobiles, and items.
- Mobile/item relations are persisted by serial references:
  - `UOMobileEntity.BackpackId`
  - `UOMobileEntity.EquippedItemIds`
  - `UOItemEntity.ParentContainerId` + `ContainerPosition`
  - `UOItemEntity.EquippedMobileId` + `EquippedLayer`

Runtime behavior:

- On startup, `IPersistenceService.StartAsync()` loads snapshot (if present) and replays journal.
- During runtime, repositories append operations to journal.
- On save/stop, `SaveSnapshotAsync()` writes a new snapshot and resets the journal.

Storage location:

- Files are written under the server `save` directory (`DirectoriesConfig[DirectoryType.Save]`).

Query support:

- `IAccountRepository`, `IMobileRepository`, and `IItemRepository` expose `QueryAsync(...)`.
- Queries are evaluated on immutable snapshots with ZLinq-backed projection/filtering.

## Templates

Moongate loads gameplay templates from `DirectoriesConfig[DirectoryType.Templates]`:

- `templates/items/**/*.json` -> loaded by `ItemTemplateLoader` into `IItemTemplateService`
- `templates/mobiles/**/*.json` -> loaded by `MobileTemplateLoader` into `IMobileTemplateService`

Template values are data-driven and resolved at runtime using spec objects:

- `HueSpec`: supports fixed values (`"4375"`, `"0x1117"`) and ranges (`"hue(5:55)"`)
- `GoldValueSpec`: supports fixed values (`"0"`) and dice notation (`"dice(1d8+8)"`)

Example item template:

```json
{
  "type": "item",
  "id": "leather_backpack",
  "name": "Leather Backpack",
  "category": "Container",
  "itemId": "0x0E76",
  "hue": "hue(10:80)",
  "goldValue": "dice(2d8+12)",
  "lootType": "Regular",
  "stackable": false,
  "isMovable": true
}
```

Example startup item template:

```json
{
  "type": "item",
  "id": "inner_torso",
  "category": "Start Clothes",
  "itemId": "0x1F7B",
  "hue": "4375",
  "goldValue": "dice(1d4+1)",
  "weight": 1
}
```

Example mobile template:

```json
{
  "type": "mobile",
  "id": "orione",
  "name": "Orione",
  "category": "animals",
  "body": "0xC9",
  "skinHue": 779,
  "hairStyle": 0,
  "brain": "orion"
}
```

Resolution model:

- JSON loading parses to typed specs (`HueSpec`, `GoldValueSpec`)
- final random values are resolved when creating runtime entities (not at JSON load time)

## Solution Structure

- `src/Moongate.Server`: host/bootstrap, game loop, network orchestration, session/event services.
- `src/Moongate.Network.Packets`: packet contracts, descriptors, registry, packet definitions.
- `src/Moongate.Generators`: unified source generators for packets, handlers, metrics, script-module registry, and version metadata.
- `src/Moongate.UO.Data`: UO domain data types and utility models.
- `src/Moongate.Core`: shared low-level utilities.
- `src/Moongate.Network`: TCP/network primitives.
- `src/Moongate.Scripting`: Lua engine service, script modules, script loaders, and scripting helpers.
- `src/Moongate.Server/Http`: embedded ASP.NET Core host service used by the server bootstrap.
- `tests/Moongate.Tests`: unit tests.
- `benchmarks/Moongate.Benchmarks`: BenchmarkDotNet performance suite.
- `docs/`: Obsidian knowledge base (plans, sprints, protocol notes, journal).

## Source Generators (AOT)

Moongate uses source generators to reduce runtime reflection/discovery work and improve Native AOT compatibility and startup performance.

Current generator project:

- `Moongate.Generators`
  - Generates packet table/registry wiring and `PacketDefinition` constants from packet metadata.
  - Generates bootstrap packet-listener registrations from `[RegisterPacketHandler(...)]`.
  - Generates bootstrap game-event-listener subscriptions from `[RegisterGameEventListener]`.
  - Generates bootstrap file-loader registrations from `[RegisterFileLoader(order)]`.
  - Generates metric snapshot mappers from metric-decorated models.
  - Generates script module registries from `[ScriptModule(...)]` in `Moongate.Scripting` and `Moongate.Server`.
  - Generates `VersionUtils` metadata for server version/codename.

Why this helps for AOT:

- Moves dynamic mapping logic from runtime to compile time.
- Reduces dependency on reflection-based registration paths.
- Improves deterministic startup behavior.

## Event And Packet Separation

Moongate uses a strict separation between inbound protocol parsing and outbound event projections:

- `IPacketListener` handles inbound packets only (`Client -> Server`) and applies domain use-cases.
- Domain services publish `IGameEvent` messages through `IGameEventBusService`.
- Game event listeners are declared with `IGameEventListener<TEvent>` and auto-subscribed at bootstrap via `[RegisterGameEventListener]`.
- `IOutboundEventListener<TEvent>` handles outbound side-effects from domain events (for example enqueueing packets).
- `RegisterOutboundEventListener<TEvent, TListener>()` is the bootstrap helper to register outbound listeners as hosted services with priority.
- `IOutgoingPacketQueue` and `IOutboundPacketSender` deliver outbound packets on the game-loop/network boundary.

## Game Loop Scheduling

The server loop is timestamp-driven (monotonic `Stopwatch`) rather than fixed-sleep tick stepping:

- `GameLoopService` computes current loop timestamp and calls `ITimerService.UpdateTicksDelta(...)`.
- `TimerWheelService` accumulates elapsed milliseconds and advances only the required number of wheel ticks.
- This keeps timer semantics stable while adapting to real runtime load.
- Optional idle throttling (`Game.IdleCpuEnabled`, `Game.IdleSleepMilliseconds`) sleeps briefly when no work was processed.

## Requirements

- .NET SDK 10.0.x

## Server Startup Tutorial

This is the recommended first-time setup to run the server locally.

1. Prepare directories:
   - `MOONGATE_ROOT_DIRECTORY`: server root (config, save, logs, scripts, templates).
   - `MOONGATE_UO_DIRECTORY`: Ultima Online client data directory.
2. Export env vars:

```bash
export MOONGATE_ROOT_DIRECTORY="$HOME/moongate"
export MOONGATE_UO_DIRECTORY="/path/to/uo-client"
```

3. Restore/build/test:

```bash
dotnet restore
dotnet build
dotnet test
```

4. Start server:

```bash
dotnet run --project src/Moongate.Server
```

5. First startup behavior:
   - If `moongate.json` is missing, it is created in `MOONGATE_ROOT_DIRECTORY`.
   - Asset/data files are copied only when missing.
   - If no accounts exist, a default admin is created.

6. Optional admin credentials override:

```bash
export MOONGATE_ADMIN_USERNAME="admin"
export MOONGATE_ADMIN_PASSWORD="change-me-now"
```

7. Verify runtime:
   - Game TCP server: port `2593`
   - HTTP endpoints (default): `http://localhost:8088/`, `http://localhost:8088/health`, `http://localhost:8088/metrics`, `http://localhost:8088/scalar`
   - Logs: `MOONGATE_ROOT_DIRECTORY/logs`

## Quick Start

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Moongate.Server
```

By default, the server starts with packet data logging enabled in `Program.cs`.

Console logging:

- Custom Serilog console sink with output template compatible formatting.
- Level-based colored output in terminal (Spectre.Console).
- Placeholder values (message properties) highlighted with dedicated styling.
- Fixed bottom prompt row (`moongate>`) when running in an interactive terminal.

HTTP service defaults:

- `Http.IsEnabled = true`
- `Http.Port = 8088`
- `Http.IsOpenApiEnabled = true`
- Base endpoint: `/`
- Health endpoint: `/health`
- OpenAPI JSON: `/openapi/v1.json`
- Scalar UI: `/scalar`
- Users API:
  - `GET /api/users`
  - `GET /api/users/{accountId}`
  - `POST /api/users`
  - `PUT /api/users/{accountId}`
  - `DELETE /api/users/{accountId}`

## Command System

Commands are registered through `ICommandSystemService.RegisterCommand(...)` with:

- `commandName`: one command or aliases separated by `|`
- `handler`: async callback with `CommandSystemContext`
- `description`: text shown in `help`
- `source`: allowed source (`Console`, `InGame`, or both)
- `minimumAccountType`: minimum role required (`Regular`, `GameMaster`, `Administrator`)

Authorization behavior:

- Console source is always evaluated as `AccountType.Administrator`.
- In-game source is evaluated using `GameSession.AccountType` (set during login).
- If source is valid but role is too low, command execution is rejected with warning output.

Example registration:

```csharp
commandSystemService.RegisterCommand(
    "whoami|me",
    context =>
    {
        context.Print("You are connected.");
        return Task.CompletedTask;
    },
    description: "Shows basic identity information.",
    source: CommandSourceType.Console | CommandSourceType.InGame,
    minimumAccountType: AccountType.Regular
);
```

Usage:

- Console: type command directly, for example `help`.
- In-game: prefix with `.` in Unicode chat, for example `.help`.

Built-in commands:

- `help|?` -> Console + InGame, `Regular`
- `lock|*` -> Console only, `Administrator`
- `exit|shutdown` -> Console only, `Administrator`
- `add_user` -> Console + InGame, `Administrator`
- `send_target` -> InGame only, `Regular`
- `orion` -> InGame only, `Regular` (opens target cursor and spawns Orion on selected location)

## Scripting

Moongate includes a Lua scripting subsystem in `src/Moongate.Scripting`, based on MoonSharp.

- `LuaScriptEngineService` handles script execution, callbacks, constants, and function invocation.
- Script modules are exposed with attributes (`[ScriptModule]`, `[ScriptFunction]`).
- Script module registration is compile-time generated (`ScriptModuleRegistry`) and invoked from bootstrap.
- `LuaScriptLoader` resolves scripts from configured script directories.
- `.luarc` metadata generation is included to improve editor tooling.

Current automated coverage includes:

- `LuaScriptLoader` file resolution and load behavior.
- `LuaScriptEngineService` constants, callbacks, module calls, error path, and naming conversions.
- `ScriptResultBuilder` success/error contract behavior.

Example script callback (for example in `<root>/scripts/init.lua`):

```lua
function on_player_connected(p)
 log.info("Toh! un player s'e' connesso")
end
```

### NPC Brain Example (`brain_loop` + `on_speech`)

Mobile template:

```json
{
  "type": "mobile",
  "id": "orc_warrior",
  "name": "an orc warrior",
  "body": "0x11",
  "brain": "orc_warrior"
}
```

Lua script (`<root>/scripts/ai/orc_warrior.lua`):

```lua
function brain_loop(npc_id)
  while true do
    -- tactical tick sleep in milliseconds
    coroutine.yield(250)
  end
end

function on_speech(listener_npc_id, speaker_id, text, speech_type, map_id, x, y, z)
  if listener_npc_id == 0 or text == nil then
    return
  end

  if string.find(string.lower(text), "hello", 1, true) then
    log.info("NPC " .. tostring(listener_npc_id) .. " heard hello from " .. tostring(speaker_id))
  end
end
```

Notes:

- `brain` in the mobile template maps to `scripts/ai/<brain>.lua` (or explicit script path if configured in registry).
- `brain_loop` is resumed by the runner and can control next wake time via `coroutine.yield(ms)`.
- `on_speech` is invoked when the NPC hears nearby speech events.

### Item `ScriptId` Dispatch

Items can define `scriptId` in templates and runtime entities (`UOItemEntity.ScriptId`).
`IItemScriptDispatcher` builds the Lua callback name from `scriptId + hook` and dispatches `ItemScriptContext`.

Function naming convention:

- `on_item_<script_id_normalized>_<hook_normalized>`

Example:

- `scriptId = "items.healing-potion"`
- `hook = "on_use"`
- Lua function called: `on_item_items_healing_potion_on_use`

Example template:

```json
{
  "type": "item",
  "id": "healing_potion",
  "name": "a healing potion",
  "itemId": "0x0F0C",
  "scriptId": "items.healing_potion"
}
```

Example Lua:

```lua
function on_item_items_healing_potion_on_use(ctx)
  log.info("Potion used by item " .. tostring(ctx.Item.Id))
end
```

## Scripts

Repository helper scripts in `scripts/`:

- `scripts/build_image.sh`: builds the Docker image using `docker buildx`, with options for tag, platform, push, and no-cache.
- `scripts/run_aot.sh`: publishes and runs the server with NativeAOT settings for local AOT verification.
- `scripts/run_benchmarks.sh`: runs BenchmarkDotNet benchmarks (`markdown` + `csv` exporters).
- `scripts/run_benchmarks_compare.sh`: runs side-by-side `JIT vs NativeAOT` micro-benchmark comparison and writes `BenchmarkDotNet.Artifacts/results/aot-vs-jit.md`.
- `scripts/run_benchmarks_lua.sh`: runs Lua script engine benchmarks only (JIT, MoonSharp is NativeAOT-incompatible). Accepts extra BenchmarkDotNet args.

## Benchmarks

Run locally:

```bash
./scripts/run_benchmarks.sh --filter '*'
```

Latest local snapshot (`2026-02-23`, `BenchmarkDotNet 0.14.0`, macOS `Darwin 25.3.0`, Apple `M4 Max`, `.NET 10.0.3`):

| Benchmark | Mean | Allocated |
|---|---:|---:|
| `PacketParsingBenchmark.ParseLoginSeedPacket` | `94.82 ns` | `664 B` |
| `PacketSerializationBenchmark.WriteServerListPacket` | `64.19 ns` | `128 B` |
| `PacketStreamParsingBenchmark.ParseMixedPacketStreamInChunks` | `24.25 us` | `56 KB` |
| `PacketDispatchBenchmark.DispatchToThreeListeners` | `68.21 ns` | `296 B` |
| `PacketDispatchBenchmark.DispatchWithoutListeners` | `8.99 ns` | `64 B` |
| `NetworkCompressionBenchmark.Compress256Bytes` | `220.76 ns` | `-` |
| `NetworkCompressionBenchmark.CompressAndDecompress1024Bytes` | `60.03 us` | `48.10 KB` |
| `NetworkCompressionBenchmark.CompressionMiddlewareProcessSend1024Bytes` | `908.72 ns` | `1.48 KB` |
| `QueueThroughputBenchmark.OutgoingQueueEnqueueThenDrain` | `24.309 us` | `-` |
| `QueueThroughputBenchmark.MessageBusPublishThenDrain` | `9.725 us` | `-` |
| `TimerWheelBenchmark.UpdateTicksDelta` | `2.893 us` | `4.05 KB` |

### Gameplay Hot-Path Benchmarks

Run only the new gameplay-focused suites:

```bash
dotnet run -c Release --project benchmarks/Moongate.Benchmarks/Moongate.Benchmarks.csproj -- \
  --filter '*SpatialWorldServiceBenchmark*' '*ItemServiceBenchmark*' '*PacketGameplayHotPathBenchmark*'
```

Latest quick snapshot (`2026-03-02`, `BenchmarkDotNet 0.15.8`, macOS `Darwin 25.3.0`, Apple `M4 Max`, `.NET 10.0.3`, quick config `Launch=1/Warmup=1/Iteration=1`):

| Benchmark | Mean | Allocated |
|---|---:|---:|
| `SpatialWorldServiceBenchmark.AddOrUpdateMobiles (500)` | `75.939 us` | `74.56 KB` |
| `SpatialWorldServiceBenchmark.MoveMobilesAcrossSectors (500)` | `27.548 us` | `117.53 KB` |
| `SpatialWorldServiceBenchmark.GetPlayersInHotSector (500)` | `1.769 us` | `6.16 KB` |
| `SpatialWorldServiceBenchmark.AddOrUpdateMobiles (2000)` | `325.353 us` | `297.27 KB` |
| `SpatialWorldServiceBenchmark.MoveMobilesAcrossSectors (2000)` | `105.423 us` | `469.15 KB` |
| `SpatialWorldServiceBenchmark.GetPlayersInHotSector (2000)` | `1.745 us` | `6.16 KB` |
| `ItemServiceBenchmark.MoveItemBetweenContainers` | `359.772 ns` | `1.85 KB` |
| `ItemServiceBenchmark.DropItemToGroundFromContainer` | `489.566 ns` | `2.25 KB` |
| `PacketGameplayHotPathBenchmark.ParseMoveRequestPacket` | `8.930 ns` | `32 B` |
| `PacketGameplayHotPathBenchmark.ParsePickUpItemPacket` | `8.620 ns` | `32 B` |
| `PacketGameplayHotPathBenchmark.ParseDropItemPacket` | `11.192 ns` | `48 B` |
| `PacketGameplayHotPathBenchmark.ParseDropWearItemPacket` | `8.955 ns` | `32 B` |
| `PacketGameplayHotPathBenchmark.ParseMixedGameplayPacketBurst` | `10.956 ns` | `36 B` |
| `PacketGameplayHotPathBenchmark.WriteObjectInformationPacket` | `63.047 ns` | `-` |
| `PacketGameplayHotPathBenchmark.WriteDraggingOfItemPacket` | `51.664 ns` | `-` |

Notes:

- This snapshot is intended for fast regression checks, not for publication-grade comparisons.
- Use default/full BenchmarkDotNet settings for release notes and long-term trend baselines.

### Lua Script Engine

Run locally:

```bash
./scripts/run_benchmarks_lua.sh
```

> Note: MoonSharp relies on reflection and dynamic code generation — NativeAOT is not supported for this suite.

Latest local snapshot (`2026-02-25`, `BenchmarkDotNet 0.15.8`, macOS `Darwin 25.3.0`, Apple `M4 Max`, `.NET 10.0`):

| Benchmark | Mean | Allocated |
|---|---:|---:|
| `LuaScriptEngineBenchmark.ExecuteSimpleScriptCached` | `328.87 ns` | `800 B` |
| `LuaScriptEngineBenchmark.ExecuteLoopScriptCached` | `5.68 us` | `19.67 KB` |
| `LuaScriptEngineBenchmark.ExecuteSimpleScriptUncached` | `6.28 us` | `6.12 KB` |
| `LuaScriptEngineBenchmark.CallFunctionNoArgs` | `49.22 ns` | `256 B` |
| `LuaScriptEngineBenchmark.CallFunctionWithArgs` | `135.40 ns` | `864 B` |

Generated reports are stored in:

- `BenchmarkDotNet.Artifacts/results/*.md`
- `BenchmarkDotNet.Artifacts/results/*.csv`

### AOT vs JIT

Run side-by-side comparison:

```bash
./scripts/run_benchmarks_compare.sh
```

Latest comparison snapshot (`2026-02-23`, `net10.0`, Apple `M4 Max`, `osx-arm64`):

| Benchmark | JIT Mean | AOT Mean | Speedup (JIT/AOT) |
|---|---:|---:|---:|
| `Compress256Bytes` | `934.48 ns` | `319.04 ns` | `2.93x` |
| `CompressAndDecompress1024Bytes` | `59.60 us` | `102.20 us` | `0.58x` |
| `CompressionMiddlewareProcessSend1024Bytes` | `974.86 ns` | `1.34 us` | `0.73x` |
| `ParseLoginSeedPacket` | `360.97 ns` | `71.66 ns` | `5.04x` |
| `ParseMixedPacketStreamInChunks` | `26.10 us` | `37.71 us` | `0.69x` |
| `WriteServerListPacket` | `585.93 ns` | `98.31 ns` | `5.96x` |

Detailed report:

- `BenchmarkDotNet.Artifacts/results/aot-vs-jit.md`

## Docker

Build the image:

```bash
./scripts/build_image.sh -t moongate-server:local
```

Run the container:

```bash
docker run --rm -it \
  -p 2593:2593 \
  -p 8088:8088 \
  -v /path/host/moongate-root:/app \
  -v /path/host/uo-client:/uo:ro \
  --name moongate \
  moongate-server:local
```

The Docker image publishes a NativeAOT binary and runs it on Alpine (`linux-musl` runtime).
It also builds the frontend in `ui/` and serves it from `/` via the HTTP service.
Container defaults:

- `MOONGATE_ROOT_DIRECTORY=/app`
- `MOONGATE_UO_DIRECTORY=/uo`
- `MOONGATE_UI_DIST=/opt/moongate/ui/dist`

`/path/host/uo-client` must contain required UO client files (e.g. `client.exe`).

Console behavior in Docker:

- Run with `-it` to enable the interactive prompt UI (`moongate>`).
- Without TTY (`-it` omitted), logs still work but prompt interaction is disabled.

## Docker Monitoring Stack

The repository includes a complete monitoring stack under `stack/`:

- Moongate server container
- Prometheus scraping `http://moongate:8088/metrics`
- Grafana with pre-provisioned datasource and dashboard

Quick start:

```bash
cd stack
docker compose up -d --build
```

Useful endpoints:

- Grafana: `http://localhost:3000`
- Prometheus: `http://localhost:9090`
- Moongate metrics: `http://localhost:8088/metrics`

For full setup details, volumes, troubleshooting, and dashboard notes, see `stack/README.md`.

## Documentation

Project documentation (Obsidian vault) is in `docs/`.
Published documentation is available at:

- https://moongate-community.github.io/moongatev2/

- Docs home: `docs/Home.md`
- Development plan: `docs/plans/moongate-v2-development-plan.md`
- Current status snapshot: `docs/plans/status-2026-02-19.md`
- Sprint tracking: `docs/sprints/sprint-001.md`
- Sprint closeout: `docs/sprints/sprint-001-closeout-2026-02-18.md`
- Protocol notes index: `docs/protocol/README.md`

## Development Notes

- Shared build/analyzer/version settings are centralized in `Directory.Build.props`.
- Current global version baseline: `0.17.0`.
- CI validates build/tests/coverage/quality/security; release and Docker image publishing run through dedicated workflows.

## Contributing

We welcome contributions. Please fork the repository and submit pull requests with your changes.
Make sure code follows the project coding standards and includes appropriate tests.

## License

This project is licensed under the GNU General Public License v3.0 (GPL-3.0).
See `LICENSE` for details.
