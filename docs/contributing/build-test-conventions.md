# Build, test & conventions

## Build and test

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet build Moongate.slnx
dotnet test Moongate.slnx
```

The exact SDK version is pinned in `global.json`. The docs site builds with
`dotnet tool restore && dotnet docfx docs/docfx.json --serve` (see the
[README](https://github.com/moongate-community/moongate#documentation)).

## Repository layout

| Path | Contents |
|---|---|
| `src/` | The seven `Moongate.*` projects — see the [architecture overview](../under-the-hood/architecture.md#solution-layout). |
| `tests/Moongate.Tests/` | The test suite, organized **by domain** (`Core/`, `Network/`, `Server/`, `Scripting/`, `Ultima/`, `UO/`, `Data/`), with shared fixtures in `Support/`. |
| `src/Moongate.Server/Assets/` | The embedded YAML source of truth for world data and templates, seeded into the runtime root on first launch. |
| `docs/` | This documentation site (DocFX). |

## Code conventions

The authoritative, always-current rules live in
[`CODE_CONVENTION.md`](https://github.com/moongate-community/moongate/blob/main/CODE_CONVENTION.md)
at the repo root. Highlights: KISS first; one type per file; domain-first
namespaces (`Interfaces`, `Types`, `Data` buckets); enums in `Types` with a
domain prefix; tests mirror the production structure
(`tests/<Project>/<Domain>/<Subject>Tests.cs`), one test class per subject.

Commits follow **Conventional Commits**, in English.

## Documentation maintenance rule

> A PR that changes the Lua surface or a YAML schema updates the
> corresponding reference page in the same PR.

Concretely: touching a `[ScriptModule]`/`[ScriptFunction]` (or the enums
registered for Lua) means updating the matching page under
[Scripting → Reference](../scripting/index.md); changing a template DTO or
loader/validator behavior means updating the matching
[data file page](../scripting/data/item-templates.md). The docs build
(`dotnet docfx docs/docfx.json --warningsAsErrors`) runs in CI on every PR
that touches `docs/` or `src/` and fails on broken links.
