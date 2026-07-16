# Moongate

<p align="center">
  <img src="https://raw.githubusercontent.com/moongate-community/moongate/main/images/moongate_logo.png" alt="Moongate logo" width="320">
</p>

<p align="center"><strong>A modern Ultima Online server, built from scratch.</strong></p>

<p align="center">
  <a href="https://github.com/moongate-community/moongate/releases"><img src="https://img.shields.io/github/v/release/moongate-community/moongate?sort=semver" alt="Latest release"></a>
  <a href="https://github.com/moongate-community/moongate/actions/workflows/ci.yml"><img src="https://github.com/moongate-community/moongate/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://hub.docker.com/r/tgiachi/moongate-server"><img src="https://img.shields.io/docker/pulls/tgiachi/moongate-server" alt="Docker pulls"></a>
  <a href="https://hub.docker.com/r/tgiachi/moongate-server"><img src="https://img.shields.io/docker/image-size/tgiachi/moongate-server/latest" alt="Image size"></a>
</p>

<p align="center">
  <a href="https://github.com/moongate-community/moongate/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="AGPL-3.0 license"></a>
  <img src="https://img.shields.io/badge/platform-.NET%2010-blueviolet" alt=".NET 10">
  <img src="https://img.shields.io/badge/client-ClassicUO%207.x-2ea44f" alt="ClassicUO 7.x">
  <img src="https://img.shields.io/badge/scripting-Lua-yellow" alt="Lua scripting">
</p>

Moongate is an Ultima Online server emulator written from scratch in .NET 10:
a single game-loop thread, Lua scripting, YAML shard data, binary snapshot
persistence, and generated documentation for every implemented packet. It is
built on the [SquidStd](https://www.nuget.org/packages?q=SquidStd) toolkit and
targets the [ClassicUO](https://www.classicuo.eu/) 7.x client only.

> **Status: early development.** Today the server takes a 7.x client through
> login, character creation, selection and deletion, and into the world, with
> movement, items, containers and the paperdoll working. It is not a fully
> playable shard yet. The exact protocol surface lives in the
> [packet reference](https://moongate-community.github.io/moongate/packets/).

## Why Another Rewrite?

Yes, rewriting a UO server from scratch is not the most practical path. I know.
Moongate exists because starting from a blank slate is how I like to learn: it
gives me room to test architecture ideas, rebuild systems from first
principles, and understand the tradeoffs by writing the code myself. Starting
over gives me clarity, curiosity, and the calm needed to keep exploring how
things work.

## Looking For Collaborators

Moongate is young and moving fast. Contributors and reviewers are welcome.

- Issues: <https://github.com/moongate-community/moongate/issues>
- Discussions: <https://github.com/moongate-community/moongate/discussions>
- Discord: <https://discord.gg/h9UUyGqd>

## Quick Start

### Requirements

- .NET SDK 10.0+
- Docker, if you want to run the container image
- Ultima Online 7.x client data files. Moongate does not distribute them.

### Run The Server Locally

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet run --project src/Moongate.Server -- --root-directory ~/moongate --uo-directory ~/uo
```

On first boot, Moongate creates the runtime directory structure, writes a
default `moongate.yaml`, and seeds an administrator account:

```text
admin / admin
```

Change the password before using a reachable server. The UO TCP server listens
on `localhost:2593`; point ClassicUO 7.x at it.

### Run Tests

```bash
dotnet test Moongate.slnx
```

## Docker

Run the published image with persistent server data and read-only UO client
files:

```bash
docker run -d --name moongate \
  -p 2593:2593 \
  -v /path/to/uo-files:/uo:ro \
  -v moongate-data:/data \
  -e MOONGATE_ROOT=/data \
  tgiachi/moongate-server:latest --uo-directory /uo
```

Or with Docker Compose:

```yaml
services:
  moongate:
    image: tgiachi/moongate-server:latest
    command: ["--uo-directory", "/uo"]
    ports:
      - "2593:2593"
    environment:
      MOONGATE_ROOT: /data
    volumes:
      - /path/to/uo-files:/uo:ro
      - moongate-data:/data
    restart: unless-stopped

volumes:
  moongate-data:
```

Images are published to Docker Hub as `tgiachi/moongate-server` and to GHCR as
`ghcr.io/moongate-community/moongate`, tagged `latest`, `X.Y`, `X.Y.Z` and
`sha-<commit>`.

## What Is In Scope Today

- UO TCP networking with typed packet records and opcode dispatch.
- Login server flow: account auth, shard list, game-server handoff.
- Character creation, selection and deletion, and the enter-world sequence.
- Movement, items, containers with gumps, and the paperdoll.
- YAML runtime configuration through `moongate.yaml`.
- Item, mobile and loot templates as embedded YAML assets, seeded into the
  runtime data directory.
- Loot tables validated against concrete item templates at startup.
- Persistence as binary snapshots per entity kind, with no database.
- Lua scripting modules: `item`, `mobile`, `loot`, `account` and `events`,
  always dispatched on the game loop.
- Generated packet reference: every packet class carries a
  `[PacketDocumentation]` attribute and the docs are built from the code.
- CI, semantic-release, Docker images and GitHub Pages documentation.

## Project Highlights

- Two layers by design: SquidStd provides the generic server infrastructure
  (host, game loop, event bus, plugins, persistence, Lua runtime), Moongate
  adds everything Ultima Online.
- One game-loop thread owns all world mutation; packet handlers publish domain
  events and behaviour lives in subscribers.
- Runtime data is YAML-first, so shard content can be edited without
  recompiling.
- The packet documentation can never drift from the code: the generator fails
  the build when a packet is missing its metadata.

## Documentation

Published documentation: <https://moongate-community.github.io/moongate/>

Useful starting points:

- Getting started: <https://moongate-community.github.io/moongate/getting-started/>
- Lua scripting: <https://moongate-community.github.io/moongate/scripting/>
- Packet reference: <https://moongate-community.github.io/moongate/packets/>
- Architecture: <https://moongate-community.github.io/moongate/under-the-hood/architecture.html>

Build it locally:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

## Contributing

Contributions are welcome. For non-trivial changes, open an issue or
discussion first so the design can be aligned before implementation.

Before sending a pull request:

- Follow `CODE_CONVENTION.md`.
- Keep changes scoped.
- Keep tests green.
- Update docs when runtime behavior changes.

## Acknowledgements

Moongate is inspired by the Ultima Online emulator ecosystem and by the
long-running work of projects such as:

- POLServer: <https://github.com/polserver/polserver>
- ModernUO: <https://github.com/modernuo/modernuo>
- UOX3: <https://github.com/UOX3DevTeam/UOX3>

## License

Copyright 2026 Squid Development.

Moongate is licensed under the
[GNU Affero General Public License v3.0](https://github.com/moongate-community/moongate/blob/main/LICENSE).
Moongate stays open: if you distribute a modified server, or run one that
players connect to, you must publish your sources under the same license.
