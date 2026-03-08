# Solution Structure

Current project layout and responsibilities.

## Solution Tree

```
moongatev2/
├── src/
│   ├── Moongate.Abstractions
│   ├── Moongate.Core
│   ├── Moongate.Email
│   ├── Moongate.Network
│   ├── Moongate.Network.Packets
│   ├── Moongate.Generators
│   ├── Moongate.Persistence
│   ├── Moongate.Scripting
│   ├── Moongate.Server
│   ├── Moongate.Server.Metrics
│   └── Moongate.UO.Data
├── tests/
│   └── Moongate.Tests
├── benchmarks/
├── converters/
├── tools/
├── moongate_data/
├── ui/
└── docs/
```

## Module Summary

- `Moongate.Abstractions`
  - base service abstractions shared across modules.
- `Moongate.Core`
  - shared types (`Serial`, geometry, json helpers, utility classes).
- `Moongate.Email`
  - email pipeline: Scriban template rendering and SMTP/no-op senders.
- `Moongate.Network`
  - TCP transport, spans, network middleware pipeline.
- `Moongate.Network.Packets`
  - packet contracts and concrete packet implementations.
- `Moongate.Generators`
  - unified source generators for:
    - packet table/definitions
    - packet listener bootstrap registration
    - game event listener bootstrap registration
    - console command registration
    - metrics snapshot mapping
    - script module registry
    - file loader registration
    - Lua user data registration
    - server version metadata (`VersionUtils`)
- `Moongate.Persistence`
  - snapshot/journal storage and repositories.
- `Moongate.Scripting`
  - Lua engine and module bridge.
- `Moongate.Server`
  - composition root, bootstrap, runtime services and handlers.
- `Moongate.Server.Metrics`
  - metrics provider abstractions and snapshot collection.
- `Moongate.UO.Data`
  - UO entities, enums, templates, gameplay data contracts.

## Runtime Composition

`MoongateBootstrap` in `Moongate.Server` wires all services via DryIoc, then starts services ordered by registration priority.

Startup also handles:

- directory initialization
- logger setup
- config load/merge
- UO directory validation
- data asset copy bootstrap
- packet listener registration
- file loader registration
- game event listener subscription
- console command registration

---

**Previous**: [Architecture TOC](toc.yml) | **Next**: [Source Generators](generators.md)
