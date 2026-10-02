<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="220" />
</p>

<h1 align="center">Moongate</h1>

<p align="center">
  <a href="https://github.com/moongate-community/moongate/actions/workflows/ci.yml?query=branch%3Adevelop"><img src="https://img.shields.io/github/actions/workflow/status/moongate-community/moongate/ci.yml?branch=develop&amp;label=CI%20%28develop%29" alt="CI on develop"></a>
  <a href="https://github.com/moongate-community/moongate/actions/workflows/security.yml?query=branch%3Amain"><img src="https://img.shields.io/github/actions/workflow/status/moongate-community/moongate/security.yml?branch=main&amp;label=security%20%28main%29" alt="Dependency security audit on main"></a>
  <a href="https://github.com/moongate-community/moongate/releases/latest"><img src="https://img.shields.io/github/v/release/moongate-community/moongate?label=release" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue" alt="License: AGPL-3.0-or-later"></a>
</p>

<p align="center">
  <a href="https://moongate.sh/"><img src="https://img.shields.io/badge/docs-moongate.sh-3867D6" alt="Documentation at moongate.sh"></a>
  <a href="https://github.com/moongate-community/moongate/pkgs/container/moongate"><img src="https://img.shields.io/badge/ghcr.io-moongate-2496ED?logo=docker&amp;logoColor=white" alt="Container image"></a>
  <a href="https://dotnet.microsoft.com/en-us/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&amp;logoColor=white" alt=".NET 10"></a>
  <a href="docs/scripting.md"><img src="https://img.shields.io/badge/Lua-5.2-2C2D72?logo=lua&amp;logoColor=white" alt="Lua 5.2 scripting"></a>
  <a href="https://buymeacoffee.com/zk7bnrbk4i"><img src="https://img.shields.io/badge/Buy%20me%20a%20coffee-support-FFDD00?logo=buymeacoffee&amp;logoColor=black" alt="Buy me a coffee"></a>
</p>

Moongate is an open-source Ultima Online server emulator written in C# on .NET 10.
It combines Lua scripting, PostgreSQL persistence and Redis-backed login-to-game
handoff, with reusable libraries for building server tools and services.

World saves never stop the game: the game loop pauses only to copy the world into memory, about a
tenth of a second for 173,000 entities, and the save writes to PostgreSQL in the background, only
the entities that changed since the last save. See
[A save does not stop the game](docs/persistence-operations.md#a-save-does-not-stop-the-game).

Moongate is multi-shard: one login server lists any number of game servers (up to
128), each running its own world with its own database, and players pick one from
the client's server list. `standalone` mode runs the login and a single shard in
one process. See [Docker login and realms](docs/docker-login-realms.md).

## Status

**Under active development; the world is not a game yet.** Characters are
created, enter the world, walk and run, see each other and talk, and move items in
their backpack and on the ground. The world is decorated, with doors that open,
locked doors and their keys, and lights; days and nights pass, dungeons are dark,
and each region has its weather, music and season. Spawn regions fill the world with NPCs, on land
and water, and respawn them; game masters also spawn and remove NPCs by hand. NPCs
near a player run their Lua mobile script, and items react to Lua item scripts.
There is no combat, pathfinding AI, death or skill gain yet.

The networking and packet pipeline, Lua runtime, persistence infrastructure,
shard data loading, and client-file readers with movement and line-of-sight
queries are in place. See [Implementation status](docs/implementation-status.md)
for supported behavior and remaining work.

## Getting started

To run Moongate, you need your own Ultima Online client data files, PostgreSQL,
and Redis 7+. Client data is not included. The [First start guide](docs/getting-started.md)
walks through preparing the server root, configuring these dependencies, applying
database migrations, and starting the server.

Choose an installation method below. The Linux installer and container image
include the .NET runtime; building from source requires the **.NET 10 SDK**.

## Install on Linux

```sh
curl -fsSL https://moongate.sh/install.sh | sh
```

Installs the latest release into `/opt/moongate` and makes `moongate` and `mgboot`
available on your path. Supports Linux x64 and ARM64 with glibc. Keep your server
root outside the installation directory so upgrades preserve your configuration
and data. See [Install on Linux](docs/installation.md) for options, upgrades and removal.

## Docker

Every release publishes a `linux/amd64` image to
[GitHub Container Registry](https://github.com/moongate-community/moongate/pkgs/container/moongate).
See [Run with Docker](docs/docker.md) for first-start configuration, persistent
storage, Docker Compose, logs, and upgrades.

## Build from source

```sh
git clone https://github.com/moongate-community/moongate.git
cd moongate
git switch develop
dotnet build Moongate.slnx -c Release
```

`develop` contains unreleased work. Use a [release tag](https://github.com/moongate-community/moongate/releases)
to build a published version. Continue with [First start](docs/getting-started.md)
to configure and run it, or [Contributing](CONTRIBUTING.md) to work on the code.

## Scripting

Scripts run on the game loop thread in a sandboxed Lua 5.2 runtime powered by
[LuaCSharp](https://github.com/nuskey8/Lua-CSharp). Put your startup script in
`scripts/init.lua` under the server root:

```lua
-- scripts/init.lua
log.info("booted {Engine} {Version}", engine.name, engine.version)

timer.every(30, function()
    log.info("tick")
    wait(2) -- suspends this coroutine without blocking the game loop
    log.info("two seconds later")
end)
```

The runtime provides logging, timers, events, NPC and item scripts, instruction
budgets, and generated editor definitions. See [Writing Lua scripts](docs/scripting.md) for available
APIs, reload commands and configuration, and the
[package README](src/Moongate.Scripting/README.md) for C# bindings and sandbox limits.

## Server guides

| Area | Guides |
| --- | --- |
| Configuration and operation | [Server configuration](docs/server-configuration.md), [diagnostics](docs/diagnostics.md) |
| Storage | [PostgreSQL persistence and world saves](docs/persistence.md) |
| Protocol and execution | [Packets and handlers](docs/packets.md), [game loop and timers](docs/game-loop-and-timers.md) |
| Client data | [Client files, movement and line-of-sight queries](docs/world-queries.md) |
| Validation | [Test coverage](docs/test-coverage.md), [dependency security audits](docs/security-audit.md) |

## Extending Moongate

| Goal | Guides |
| --- | --- |
| Add server behavior | [Plugins](docs/plugins.md), [Lua modules in C#](docs/lua-modules.md) |
| Add diagnostics | [Metric providers](docs/metric-providers.md) |
| Define shard content | [Shard data files](docs/data-files.md), [TOML templates](docs/templates.md) |
| Customize data formats | [TOML value types](docs/toml-types.md) |
| Translate server messages | [Localization](docs/localization.md) |

The compiled [sample plugin](samples/Moongate.Sample.Plugin/) demonstrates plugin
registration, Lua bindings and a metric provider. The test suite loads it through
the real plugin loader.

## Libraries

The nine library packages have their own English READMEs and runnable examples.
See [NuGet libraries and package verification](docs/nuget-packaging.md) for the
package list, dependencies, and the local verification command.

## Documentation

Browse the full guides and library documentation at **[moongate.sh](https://moongate.sh/)**.
See the [changelog](CHANGELOG.md) for release history and
[Writing documentation](docs/documentation.md) for local previews and page contributions.

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, coding conventions,
validation commands, and the pull request workflow. Contributions target `develop`.

Moongate is also a personal project built for the enjoyment of programming.
[How I use AI](docs/ai-usage.md) explains the maintainer's approach to AI-assisted
migration, testing and design, and the code he chooses to write by hand.

## License

Moongate is licensed under [AGPL-3.0-or-later](LICENSE).
Third-party attribution is listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
