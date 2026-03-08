# Moongate v2

<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="220" />
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-.NET%2010-blueviolet" alt=".NET 10">
  <img src="https://img.shields.io/badge/AOT-enabled-green" alt="AOT Enabled">
  <img src="https://img.shields.io/badge/scripting-Lua-yellow" alt="Lua Scripting">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="GPL-3.0 License">
</p>

[![CI](https://github.com/moongate-community/moongatev2/actions/workflows/ci.yml/badge.svg)](https://github.com/moongate-community/moongatev2/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/moongate-community/moongatev2/gh-pages/badges/coverage.json)](https://github.com/moongate-community/moongatev2/actions/workflows/coverage.yml)
[![Docker Image](https://img.shields.io/docker/v/tgiachi/moongate?sort=semver)](https://hub.docker.com/r/tgiachi/moongate)

Moongate v2 is a modern Ultima Online server built with .NET 10, NativeAOT support, deterministic game-loop processing, Lua scripting, and a chunk/sector-based spatial world model.

## Looking for Collaborators

I am actively looking for contributors and reviewers.

- Issues: <https://github.com/moongate-community/moongatev2/issues>
- Discussions: <https://github.com/moongate-community/moongatev2/discussions>
- Matrix: <https://matrix.to/#/#moongate:matrix.org>

## Quick Start

### Requirements

- .NET SDK 10.0+
- Ultima Online data files (client)

### Run Server (local)

```bash
git clone https://github.com/moongate-community/moongatev2.git
cd moongatev2
dotnet run --project src/Moongate.Server -- --root-directory ~/moongate --uo-directory ~/uo
```

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
- Snapshot + journal persistence (MessagePack source-generated, AOT-safe)
- Lua scripting runtime for commands, gumps, item/mobile behavior
- HTTP admin API + OpenAPI for tooling/UI
- Web admin UI (`ui/`) for item templates and server/admin workflows

## Project Highlights

- Spatial model is sector-first (chunk-style), not pure repeated range scans.
- World generation pipeline uses named generators (`IWorldGenerator`) and command-triggered runs (example: doors).
- Doors support runtime open/close behavior and network updates.
- AOT stability issue in persistence was resolved by moving to MessagePack-CSharp source-generated contracts.

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

Published docs: <https://moongate-community.github.io/moongatev2/>

## Docker

Build image:

```bash
docker build -t moongate:local .
```

Run container:

```bash
docker run --rm -it \
  -p 2593:2593 \
  -p 8088:8088 \
  -v "$HOME/moongate:/app/moongate" \
  -v "$HOME/uo:/app/uo" \
  moongate:local
```

Official image: <https://hub.docker.com/r/tgiachi/moongate>

## Benchmarks and Stress

- Benchmarks project: `benchmarks/Moongate.Benchmarks`
- Black-box socket stress tool: `tools/Moongate.Stress`
- Guide: `docs/articles/operations/stress-test.md`

## Acknowledgements

Moongate v2 is inspired by the UO emulator ecosystem.

Special thanks:

- POLServer: <https://github.com/polserver/polserver>
- ModernUO: <https://github.com/modernuo/modernuo>

Data imported/adapted from ModernUO distribution is used in selected world datasets (decoration, locations, signs).

## Contributing

Contributions are welcome. Please open an issue/discussion first for non-trivial changes.

- Follow `CODE_CONVENTION.md`
- Keep tests green
- Keep docs aligned with runtime behavior

## License

GPL-3.0. See `LICENSE`.
