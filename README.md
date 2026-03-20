# Moongate v2

<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="220" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-.NET%2010-blueviolet" alt=".NET 10">
  <img src="https://img.shields.io/badge/scripting-Lua-yellow" alt="Lua Scripting">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="GPL-3.0 License">
</p>

[![CI](https://github.com/moongate-community/moongate/actions/workflows/ci.yml/badge.svg)](https://github.com/moongate-community/moongate/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongate/gh-pages/badges/tests.json)](https://github.com/moongate-community/moongate/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongate/gh-pages/badges/coverage.json)](https://github.com/moongate-community/moongate/actions/workflows/coverage.yml)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fmoongate-community%2Fmoongate.svg?type=small)](https://app.fossa.com/projects/git%2Bgithub.com%2Fmoongate-community%2Fmoongate?ref=badge_small)
[![Docker Image](https://img.shields.io/docker/v/tgiachi/moongate?sort=semver)](https://hub.docker.com/r/tgiachi/moongate)

Moongate v2 is a modern Ultima Online server built with .NET 10, deterministic game-loop processing, Lua scripting, and a chunk/sector-based spatial world model.


[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fmoongate-community%2Fmoongate.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fmoongate-community%2Fmoongate?ref=badge_large)

## Looking for Collaborators

I am actively looking for contributors and reviewers.

- Issues: <https://github.com/moongate-community/moongate/issues>
- Discussions: <https://github.com/moongate-community/moongate/discussions>
- Discord: <https://discord.gg/h9UUyGqd>

## Release Automation

- Pull requests run the `CI` workflow only, to keep feedback fast.
- Coverage, security audit, and third-party notices run on `main`, with scheduled/manual execution for the heavier audits.
- GitHub releases are mirrored to Discord via the `GitHub Release Assets` workflow.
- Configure the repository secret `DISCORD_CHANGELOG_WEBHOOK_URL` to enable changelog posting.
- Release notes are sourced from `CHANGELOG.md` and posted to Discord after the GitHub release is created.
- NuGet packages are published by the `NuGet Publish` workflow.
- Configure the repository secret `NUGET_KEY` to enable publishing to `nuget.org`.

## Quick Start

### Requirements

- .NET SDK 10.0+
- Ultima Online data files (client)

### Run Server (local)

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet run --project src/Moongate.Server -- --root-directory ~/moongate --uo-directory ~/uo
```

### Run Server (Docker quick start)

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
docker build -t moongate:local .
docker run --rm -it \
  -p 2593:2593 \
  -p 8088:8088 \
  -v "$HOME/moongate:/app/moongate" \
  -v "$HOME/uo:/app/uo" \
  moongate:local
```

Server port: `2593`  
UDP ping port: `12000`  
HTTP/UI API port: `8088`

Default credentials: `admin` / `password`  
Change them immediately from the server console with `.password`.

Player portal routes:

- Dev UI: `http://localhost:5173/portal/login`
- Embedded UI: `http://localhost:8088/portal/login`
- Authenticated profile: `http://localhost:8088/portal/profile`

### Run UI (dev)

```bash
cd ui
npm install
npm run dev
```

UI default URL: `http://localhost:8088/`

## What Is In Scope Today

- UO TCP server + packet pipeline
- Deterministic single game-loop with separate network inbound/outbound workers
- Source-generated packet/command/listener registration
- Sector/chunk spatial system with lazy warmup and broadcast radius
- Registry-driven snapshot buckets + journal persistence over MemoryPackable runtime entities
- Lua scripting runtime for commands, gumps, item/mobile behavior
- Startup-loaded C# plugins from the runtime `plugins/<plugin-id>/` directory
- Classic books rendered from `moongate_data/templates/books/*.txt`, with support for both read-only content and writable books (`0x93` header saves, `0x66` page saves)
- HTTP admin API + OpenAPI for tooling/UI
- Web admin UI (`ui/`) for item templates and server/admin workflows
- Player portal for authenticated account overview, profile editing, and password change (`/portal/login`, `/portal/profile`, `/api/portal/me`, `/api/portal/me/password`)

## Project Highlights

- Spatial model is sector-first (chunk-style), not pure repeated range scans.
- World generation pipeline uses named generators (`IWorldGenerator`) and command-triggered runs (example: doors).
- Doors support runtime open/close behavior and network updates.
- Persistence uses MemoryPack with registry-driven snapshot buckets and generic journal entries.
- Mobile domain model was refactored to remove `Level` and `Experience` from `UOMobileEntity` (they are not part of the base UO mobile model).

## Screenshots

### Web Admin UI

- **UI Screen 1**: login and initial admin entry point.
  ![UI Screen 1](images/ui/ui_screen1.png)
- **UI Screen 2**: authenticated dashboard and main navigation.
  ![UI Screen 2](images/ui/ui_screen2.png)
- **UI Screen 3**: item templates search with image previews.
  ![UI Screen 3](images/ui/ui_screen_3.png)

### In-Game Features

- **Character Creator at Docks**: character creation flow and initial spawn area.
  ![Character Creator at Docks](images/screenshots/screen_creator_at_docks.png)
- **Door Open/Close Fix**: the bug is still there (damn doors).
  ![Door Open/Close Fix](images/screenshots/screen_door_bug.png)
- **Orion Lua Brain**: scripted NPC behavior example (`orion.lua`) with speech loop (my cat is always hungry and always looking for food).
  ![Orion Lua Brain](images/screenshots/screen_orione_hungry_cat.png)
- **Teleport Gump**: Lua-driven teleport UI and location workflow.
  ![Teleport Gump](images/screenshots/screen_teleport_gump.png)

## Documentation

- Docs home: `docs/index.md`
- Getting started: `docs/articles/getting-started/`
- Architecture: `docs/articles/architecture/`
- Scripting: `docs/articles/scripting/`
- Persistence: `docs/articles/persistence/`
- Networking/protocol: `docs/articles/networking/`
- Operations/stress test: `docs/articles/operations/stress-test.md`

Published docs: <https://moongate-community.github.io/moongate/>

## Benchmarks and Stress

- Benchmarks project: `benchmarks/Moongate.Benchmarks`
- Cross-map teleport cold-destination benchmark: `TeleportMapChangeBenchmark.HandleCrossMapTeleport_ColdDestination`
- Same-map teleport cold-destination benchmark with self refresh: `TeleportMapChangeBenchmark.HandleSameMapTeleport_ColdDestination_WithSelfRefresh`
  - Dry run on Apple M4 Max / .NET 10:
    - cross-map median `2.850 ms`, mean `4.284 ms`, first cold outlier `19.939 ms`, allocated `1.85 MB`
    - same-map median `1.947 ms`, mean `2.908 ms`, first cold outlier `13.514 ms`, allocated `1.22 MB`
- Intelligent NPC dialogue scheduling benchmark: `NpcDialogueSchedulingBenchmark`
  - Dry run on Apple M4 Max / .NET 10:
    - queue listener median `2.729 us`, mean `183.0 us`, first cold outlier `2.133 ms`, allocated `592 B`
    - schedule and complete median `1.170 ms`, mean `1.258 ms`, first cold outlier `2.169 ms`, allocated `1552 B`
    - reject duplicate in-flight median `1.270 ms`, mean `1.073 ms`, first cold outlier `2.696 ms`, allocated `1288 B`
- Black-box socket stress tool: `tools/Moongate.Stress`
- Guide: `docs/articles/operations/stress-test.md`
- Packet handler performance guide: `docs/articles/networking/packet-handler-performance.md`

## Acknowledgements

Moongate v2 is inspired by the UO emulator ecosystem.

Special thanks:

- POLServer: <https://github.com/polserver/polserver>
- ModernUO: <https://github.com/modernuo/modernuo>

Data imported/adapted from ModernUO distribution is used in selected world datasets (decoration, locations, signs).

## Star History

[![Star History Chart](https://api.star-history.com/image?repos=moongate-community/moongate&type=date&legend=top-left)](https://www.star-history.com/?repos=moongate-community%2Fmoongate&type=date&legend=top-left)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fmoongate-community%2Fmoongate.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fmoongate-community%2Fmoongate?ref=badge_shield)

## Contributing

Contributions are welcome. Please open an issue/discussion first for non-trivial changes.

- Follow `CODE_CONVENTION.md`
- Keep tests green
- Keep docs aligned with runtime behavior

## License

GPL-3.0. See `LICENSE`.
