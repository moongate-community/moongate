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
- [Current Status](#current-status)
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
- [Scripts](#scripts)
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
- Embedded HTTP host (`Moongate.Server.Http`) for health/admin endpoints and OpenAPI/Scalar docs.
- Dedicated HTTP rolling logs in the shared logs directory (`moongate_http-*.log`).
- Snapshot+journal persistence module (`Moongate.Persistence`) integrated in server lifecycle.
- ID-based persistence references for character equipment/container ownership.
- Interactive console UI with fixed prompt (`moongate>`) and Spectre-based colored log rendering.
- Timer wheel runtime metrics integrated in the metrics pipeline (`timer.*`).
- Timestamp-driven game loop scheduling with timer delta updates and optional idle CPU throttling.

For a detailed internal status snapshot, see `docs/plans/status-2026-02-19.md`.

## UO Feature Support (Current)

This section reflects the current server-side implementation status.

### Supported now

- Login/auth handshake:
  - `0xEF` Login Seed
  - `0x80` Account Login
  - `0xA0` Server Select
  - `0x91` Game Login
  - `0x5D` Login Character
  - denial flow with `LoginDeniedPacket` when credentials are invalid.
- Character lifecycle:
  - character creation (`0x00`) and persistence.
  - account -> character linkage and character selection.
- After-login world bootstrap:
  - login confirm / support features / draw player.
  - mobile draw + worn items + backpack container draw.
  - warmode, light, season, login complete, time, paperdoll.
- Movement:
  - move request (`0x02`) with sequence/throttle checks.
  - move confirm / move deny responses.
- Player status:
  - get player status (`0x34`, basic status path) -> status response (`0x11`).
- Speech:
  - Unicode speech inbound (`0xAD`).
  - local echo response and in-game command dispatch for messages starting with `.`.
- Ping:
  - ping message (`0x73`) request/response.
- Tooltips (MegaCliloc):
  - inbound request (`0xD6`) for item/mobile serials.
  - outbound object property list (`0xD6`) response.

### Partially implemented

- Packet model coverage is broader than runtime listener coverage.
  - Many packets exist in `Moongate.Network.Packets` and can parse/write.
  - Only packets bound to active listeners are currently used by gameplay flow.
- HTTP administration/metrics/OpenAPI are available, but gameplay admin features are still minimal.
- Lua scripting runtime is integrated, but gameplay script surface is still growing.

### Not yet implemented (major areas)

- Full combat loop (swing/spell damage pipeline, notoriety-driven combat rules).
- Skill system execution and progression.
- Item interaction core (pickup/drop/use/equip transaction flow across all cases).
- NPC AI, vendors, loot systems, pathfinding, spawn regions.
- World simulation breadth (housing, boats, advanced map interactions, seasons/weather effects gameplay-side).
- Economy systems, trading, banking behavior completeness.
- Full UO protocol coverage in listeners (many opcodes still intentionally unhandled).

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
- `src/Moongate.Network.Packets.Generators`: source generator for packet table registration.
- `src/Moongate.Server.PacketHandlers.Generators`: source generator for packet listener bootstrap registration.
- `src/Moongate.Server.Metrics.Generators`: source generator for metric snapshot mapping.
- `src/Moongate.UO.Data`: UO domain data types and utility models.
- `src/Moongate.Core`: shared low-level utilities.
- `src/Moongate.Network`: TCP/network primitives.
- `src/Moongate.Scripting`: Lua engine service, script modules, script loaders, and scripting helpers.
- `src/Moongate.Server.Http`: embedded ASP.NET Core host service used by the server bootstrap.
- `tests/Moongate.Tests`: unit tests.
- `docs/`: Obsidian knowledge base (plans, sprints, protocol notes, journal).

## Source Generators (AOT)

Moongate uses source generators to reduce runtime reflection/discovery work and improve Native AOT compatibility and startup performance.

Current generators:

- `Moongate.Network.Packets.Generators`
  - Generates packet table/registry wiring from packet metadata.
- `Moongate.Server.PacketHandlers.Generators`
  - Generates bootstrap packet listener registrations from `[RegisterPacketHandler(...)]` attributes.
  - Supports multiple opcode attributes on the same listener class.
- `Moongate.Server.Metrics.Generators`
  - Generates metric snapshot mappers from metric-decorated snapshot models.

Why this helps for AOT:

- Moves dynamic mapping logic from runtime to compile time.
- Reduces dependency on reflection-based registration paths.
- Improves deterministic startup behavior.

## Event And Packet Separation

Moongate uses a strict separation between inbound protocol parsing and outbound event projections:

- `IPacketListener` handles inbound packets only (`Client -> Server`) and applies domain use-cases.
- Domain services publish `IGameEvent` messages through `IGameEventBusService`.
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
   - HTTP endpoints (default): `http://localhost:8088/health`, `http://localhost:8088/metrics`, `http://localhost:8088/scalar`
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

## Scripting

Moongate includes a Lua scripting subsystem in `src/Moongate.Scripting`, based on MoonSharp.

- `LuaScriptEngineService` handles script execution, callbacks, constants, and function invocation.
- Script modules are exposed with attributes (`[ScriptModule]`, `[ScriptFunction]`).
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

## Scripts

Repository helper scripts in `scripts/`:

- `scripts/build_image.sh`: builds the Docker image using `docker buildx`, with options for tag, platform, push, and no-cache.
- `scripts/run_aot.sh`: publishes and runs the server with NativeAOT settings for local AOT verification.

## Docker

Build the image:

```bash
./scripts/build_image.sh -t moongate-server:local
```

Run the container:

```bash
docker run --rm -it \
  -p 2593:2593 \
  -v /path/host/moongate-root:/app \
  -v /path/host/uo-client:/uo:ro \
  --name moongate \
  moongate-server:local
```

The Docker image publishes a NativeAOT binary and runs it on Alpine (`linux-musl` runtime).
Container defaults:

- `MOONGATE_ROOT_DIRECTORY=/app`
- `MOONGATE_UO_DIRECTORY=/uo`

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

- Docs home: `docs/Home.md`
- Development plan: `docs/plans/moongate-v2-development-plan.md`
- Current status snapshot: `docs/plans/status-2026-02-19.md`
- Sprint tracking: `docs/sprints/sprint-001.md`
- Sprint closeout: `docs/sprints/sprint-001-closeout-2026-02-18.md`
- Protocol notes index: `docs/protocol/README.md`

## Development Notes

- Shared build/analyzer/version settings are centralized in `Directory.Build.props`.
- Current global version baseline: `0.9.0`.
- CI validates build/tests/coverage/quality/security; release and Docker image publishing run through dedicated workflows.

## Contributing

We welcome contributions. Please fork the repository and submit pull requests with your changes.
Make sure code follows the project coding standards and includes appropriate tests.

## License

This project is licensed under the GNU General Public License v3.0 (GPL-3.0).
See `LICENSE` for details.
