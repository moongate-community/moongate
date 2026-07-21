# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Moongate is a modern Ultima Online server, written from scratch in C# (.NET 10) on top of the
**SquidStd** framework (a set of NuGet packages providing the bootstrap, DI, plugin loader, config,
persistence and networking primitives). Game content is authored in Lua and YAML; it targets the
ClassicUO 7.x client only.

## Commands

SDK is pinned to **10.0.301** in `global.json`. The solution file is `Moongate.slnx` (SLNX format).

```bash
# Build / test the whole solution
dotnet build Moongate.slnx
dotnet test Moongate.slnx

# Run a single test class or method (xUnit)
dotnet test Moongate.slnx --filter "FullyQualifiedName~<ClassName>"
dotnet test Moongate.slnx --filter "Name=<TestMethodName>"

# Run the server locally (creates the runtime dir + a default moongate.yaml + admin/admin on first boot)
dotnet run --project src/Moongate.Server -- --root-directory ~/moongate --uo-directory ~/uo
# scripts/run_server.sh does a self-contained publish+run for the current RID (AOT is NOT supported)

# Docs site (DocFX). CI runs it with --warningsAsErrors, so it MUST build at 0 warnings.
dotnet tool restore
dotnet docfx docs/docfx.json --warningsAsErrors     # build (gate)
dotnet docfx docs/docfx.json --serve                # preview at http://localhost:8080
```

Analyzers run during the build (`EnableNETAnalyzers` + `EnforceCodeStyleInBuild` in
`Directory.Build.props`); `TreatWarningsAsErrors` is **false**, so warnings do not fail the .NET
build — but the DocFX build does fail on any warning.

## Architecture

**Bootstrap.** `src/Moongate.Server/Program.cs` builds a SquidStd bootstrap: it registers plugins
via `stdBootstrap.UsePlugins(builder => { builder.FromDirectory("plugins"); builder.Add<T>(); ... })`,
then `ConfigureServices` on a **DryIoc** container. Config is "config-first": a `RegisterConfigSection<T>("x")`
binds a section of `moongate.yaml` eagerly at registration. Everything the server does — networking,
commands, packet handlers, the HTTP API, the admin console — is wired through this plugin mechanism.

**Projects (`src/`).** Each is one bounded responsibility; dependencies flow one way:
- `Moongate.Core` — domain primitives (Serial, geometry, `Types` enums), extensions.
- `Moongate.Ultima` — **standalone** reader of UO client MUL/UOP files (maps, art, gumps, anim,
  hues, tiledata) via process-wide statics. It must **not** reference `Moongate.Core` or any other
  Moongate project; do not relocate its types into Core to dedupe.
- `Moongate.UO.Data` — UO data models (bodies, skills, tiles). References Core + Ultima.
- `Moongate.Network` — typed packet records under `Packets/` and opcode dispatch.
- `Moongate.Persistence` — entity stores over SquidStd.Persistence (MessagePack).
- `Moongate.Server.Abstractions` — the **contract assembly**: every service interface, domain event,
  session model, config record, `Types` enum, and the registration-seam extension methods. Plugins
  depend on this, **never on `Moongate.Server`**.
- `Moongate.Server` — the composition root and concrete service implementations.
- `Moongate.Scripting` — the Lua runtime and script modules (game-content authoring, no recompile).

**Plugins (`plugins/`).** External-DLL plugins implement `ISquidStdPlugin` (`Metadata` +
`Configure(IContainer, PluginContext)`), have a public parameterless constructor, and are loaded
into the **default** AssemblyLoadContext (no version isolation — build against the exact host
assembly versions). `Moongate.Http.Plugin` is the REST API surface; `Moongate.Console.Admin.Plugin`
is the telnet admin console. Registration seams a plugin's `Configure` calls:
`RegisterConfigSection`/`RegisterConfigFile` + `RegisterStdService` (SquidStd.Abstractions),
`RegisterCommand`/`RegisterPacketHandler`/`RegisterEventSubscriber`/`RegisterDataLoader`
(`Moongate.Server.Abstractions.Extensions`), and `RegisterApiEndpoint` (`Moongate.Http.Plugin`).
See `docs/contributing/writing-plugins/` for the full walkthrough.

**Game loop.** There is a single game-loop thread. Domain-event subscribers run on it (safe to touch
world state directly); background work off the loop (a hosted `ISquidStdService`, a socket) must
marshal onto it via `IMainThreadDispatcher`.

**World data.** The source of truth for templates and world data is the tracked, embedded YAML under
`src/Moongate.Server/Assets/` (loaded by `IDataLoader`s in priority order at startup and seeded into
the runtime root on first boot). The runtime working directory is `moongate_root/`; `moongate/` is a
gitignored leftover with the same shape. When adding a template, bump the hardcoded counts in the
relevant tests.

## Conventions

The authoritative C# style rules are in **`CODE_CONVENTION.md`** at the repo root (folder-to-namespace
mapping; domain-first `Types`/`Data`/`Interfaces` buckets; one type per file; class layout = private
readonly → props → constructor; `Dispose` last; no primary or expression-bodied constructors; `///`
on interfaces). Note that CODE_CONVENTION.md §10–12 (plugin/D-Bus/hosted-service) predates the current
SquidStd architecture — trust the actual code and the plugin docs over those sections.

Tests live in `tests/Moongate.Tests/<Domain>/<Subdomain>/<Subject>Tests.cs` with a namespace matching
the folder path; shared fixtures go in `Support/`. Commit messages are Conventional Commits, in
English.

## Packets and docs (must stay in sync)

- **Every packet class change** requires rerunning the packet-docs generator from the repo root and
  committing the regenerated `docs/packets/` markdown:
  `dotnet run scripts/generate-packet-docs.cs`. Packet classes MUST carry
  `[PacketDocumentation(family, Length/IsVariableLength, SubCommand?, Name?)]` or generation and tests
  fail.
- REST endpoint handlers must be method groups (not lambdas) with `///` summaries — Swashbuckle only
  reads XML docs off method groups, and they render as the public API reference.
- Before pushing a feature, check whether `docs/` needs updates (scripting reference, guides,
  data-template pages). Docs deploy to **moongate.sh only from `main`**, so docs merged to `develop`
  are not yet live.

## Development workflow

Every feature follows the same flow:

1. **Open a GitHub issue first**, with a **type** label (`Bug`, `feature`, `improvement`) and an
   **area** label (`backend`, `client`, `network`, …), and an accurate description of the change.
2. **Branch from `develop`.** Create a `feature/<name>` branch off the latest `develop` — never work
   directly on `develop` or `main`. (Isolated worktrees under `.claude/worktrees/` are used for this.)
3. Implement the change, committing in **English Conventional Commits** (no AI attribution).
4. **Check the network layer.** If any packet class changed, regenerate the packet reference and
   commit it — `dotnet run scripts/generate-packet-docs.cs` — and keep each packet's
   `[PacketDocumentation]` attribute (see §11 / the Packets section).
5. **Keep the docs coherent.** Update `docs/` for any behaviour or API change, and confirm
   `dotnet docfx docs/docfx.json --warningsAsErrors` builds at **0 warnings**.
6. **Merge via PR into `develop`.** Open a PR from the feature branch to `develop` with a clear
   description; merge once CI is green, then delete the branch.
7. **Close the issue.** Once the PR is merged into `develop`, close its issue by hand, naming the PR
   and the merge commit. A `Closes #N` keyword in the PR body is **not** enough: GitHub only acts on
   it when a PR merges into the **default branch**, which here is `main`, so an issue integrated
   through `develop` stays open until the next release unless it is closed explicitly.

Releases are separate (see below): they go through a `develop`→`main` PR labelled `release`.

## Release

`develop` is the integration branch; features branch off it and PR back. Releases go via a
develop→main PR labelled `release`: semantic-release derives the version from Conventional Commits,
publishes container images to GHCR + Docker Hub (`tgiachi/moongate-server`), publishes the
`Moongate.Server.Abstractions` package closure (6 packages: Core, Network, Persistence, UO.Data,
Ultima, Server.Abstractions) to the `moongate-community` GitHub Packages NuGet feed, and deploys the
docs. Only those 6 projects are packable (`<IsPackable>true</IsPackable>`); everything else defaults
to non-packable in `Directory.Build.props`.
