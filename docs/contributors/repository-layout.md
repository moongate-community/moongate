# Repository layout

`Moongate.slnx` contains seven source projects and one test project. The descriptions below follow the project references and the namespaces contained in each project.

## Source projects

- `Moongate.Core` contains shared geometry, primitives, types, extensions, and the game-loop context contract. It has no project references.
- `Moongate.Ultima` reads and renders Ultima client formats, including maps, tiles, animation, art, fonts, audio, localization, and UOP/MUL files. It has no project references.
- `Moongate.UO.Data` defines game-data models and protocol-related types for bodies, items, loot, locations, professions, regions, skills, and other Ultima Online data. It references `Moongate.Core` and `Moongate.Ultima`.
- `Moongate.Network` implements Ultima Online packet models, framing, compression, packet registration, and protocol metadata. It references `Moongate.Core` and `Moongate.UO.Data`.
- `Moongate.Persistence` contains persisted account and mobile entities, serial generators, and the persistence plugin. It references `Moongate.Core`, `Moongate.UO.Data`, and `Moongate.Ultima`.
- `Moongate.Scripting` provides the scripting plugin and Lua-facing game-loop and logging modules. It references `Moongate.Core`.
- `Moongate.Server` is the executable host. Its namespaces contain configuration, handlers, data loaders, services, session and handshake state, and embedded server data. It references `Moongate.Network`, `Moongate.Persistence`, and `Moongate.Scripting`.

See the [Architecture overview](../architecture/index.md) for runtime relationships rather than treating project references alone as a runtime design diagram.

## Tests and supporting files

- `tests/Moongate.Tests` is the xUnit test project. It references Core, Network, Server, Ultima, and UO.Data. Tests for Ultima file readers construct synthetic on-disk fixtures in temporary directories; the verified suite does not require proprietary client files.
- `docs` contains the VitePress documentation site.
- `.github/workflows` contains repository automation present on the branch.
- `Directory.Build.props` supplies solution-wide .NET defaults, analyzer settings, package metadata, and Release build settings.
- `global.json` selects the .NET SDK.
