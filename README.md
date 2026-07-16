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

Moongate is an Ultima Online server emulator written from scratch in .NET 10
on the [SquidStd](https://www.nuget.org/packages?q=SquidStd) toolkit: a single
game-loop thread, Lua scripting, YAML templates and binary snapshot
persistence. It targets the [ClassicUO](https://www.classicuo.eu/) 7.x client
only.

> **Status: early development.** Today the server takes a 7.x client through
> login, character creation/selection/deletion and into the world, with
> movement, items & containers and the paperdoll working. It is not a fully
> playable shard yet. The exact protocol surface lives in the
> [packet reference](https://moongate-community.github.io/moongate/packets/).

## Looking for collaborators

Moongate is young and moving fast — contributors and reviewers are welcome.

- Issues: <https://github.com/moongate-community/moongate/issues>
- Discussions: <https://github.com/moongate-community/moongate/discussions>
- Discord: <https://discord.gg/h9UUyGqd>

## Quick start (Docker)

You need your own **Ultima Online 7.x client files** — Moongate does not
distribute them.

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

The first run generates `moongate.yaml` (the whole configuration) inside the
data root and seeds a default `admin` account — see
[getting started](https://moongate-community.github.io/moongate/getting-started/).
Point ClassicUO 7.x at port `2593`.

Images are published on
[Docker Hub](https://hub.docker.com/r/tgiachi/moongate-server)
(`tgiachi/moongate-server`) and
[GHCR](https://github.com/moongate-community/moongate/pkgs/container/moongate)
(`ghcr.io/moongate-community/moongate`) with `latest`, `X.Y`, `X.Y.Z` and
`sha-<commit>` tags.

## Features

- **Classic UO wire protocol** as small typed packet records — every
  implemented packet is documented in the generated
  [packet reference](https://moongate-community.github.io/moongate/packets/)
- **Lua scripting** — `item`, `mobile`, `loot`, `account` and `events`
  modules, always dispatched on the game loop
  ([scripting guide](https://moongate-community.github.io/moongate/scripting/))
- **YAML templates** for items, mobiles and loot tables
  ([data reference](https://moongate-community.github.io/moongate/scripting/data/item-templates.html))
- **Binary snapshot persistence** (MessagePack) — no database, no ORM
- **Domain events** on an event bus: packet handlers stay thin, behaviour
  lives in subscribers
- **Single game-loop thread** — everything that mutates the world runs
  loop-affine

## Building from source

Requires the .NET 10 SDK.

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet build Moongate.slnx
dotnet test Moongate.slnx
dotnet run --project src/Moongate.Server -- --root-directory ~/moongate --uo-directory ~/uo
```

## Documentation

The full documentation lives at
**<https://moongate-community.github.io/moongate/>** — getting started,
scripting guides, packet reference and architecture notes. To build it
locally:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

## License

Copyright 2026 Squid Development.

Licensed under the
[GNU Affero General Public License v3.0](https://github.com/moongate-community/moongate/blob/main/LICENSE):
Moongate stays open — if you distribute a modified server, or run one that
players connect to, you must publish your sources under the same license.
