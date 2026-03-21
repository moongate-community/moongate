# Introduction to Moongate v2

## What is Moongate v2?

**Moongate v2** is a modern, high-performance Ultima Online server built from the ground up with **.NET 10**. It represents a complete rewrite of the original Moongate project, focusing on clean architecture, explicit boundaries, and practical performance.

## Project Vision

Moongate v2 is not a clone of ModernUO, RunUO, ServUO, or any other server. While we owe inspiration to these projects and their invaluable contributions to the UO community, Moongate v2 follows its own path:

### Core Principles

1. **Performance First** - Leveraging .NET 10 and explicit architecture for predictable performance
2. **Explicit Architecture** - Clear boundaries between networking, game logic, and persistence
3. **Thread Safety** - Deterministic game-loop processing with safe cross-thread communication
4. **Modern Tooling** - Source generators, typed packet definitions, OpenAPI documentation
5. **Accessible Scripting** - Lua-based customization for server administrators

## Key Technologies

| Technology | Purpose |
|------------|---------|
| **.NET 10** | Latest .NET runtime with performance improvements |
| **MoonSharp** | Lua scripting engine for gameplay customization |
| **Serilog** | Structured logging with console and file sinks |
| **Spectre.Console** | Rich terminal UI with colored output |
| **MemoryPack** | Binary serialization for runtime entity persistence |
| **ZLinq** | LINQ-like queries for data repositories |

## Architecture Highlights

### Network Layer

- Custom TCP server optimized for UO protocol
- Packet framing and parsing for fixed/variable sizes
- Source-generated packet registration via `[PacketHandler]` attributes
- Inbound message bus for network → game-loop communication

### Game Loop

- Timestamp-driven scheduling (monotonic `Stopwatch`)
- Timer wheel for efficient event scheduling
- Optional idle CPU throttling
- Deterministic tick processing

### Event System

- Strict separation: inbound packets vs outbound events
- `IPacketListener` for client → server handling
- `IGameEventBusService` for domain event publishing
- `IOutboundEventListener<TEvent>` for event → network side effects

### Persistence

- Snapshot file (`world.snapshot.bin`) for full state checkpoints
- Append-only journal (`world.journal.bin`) for incremental changes
- MemoryPack binary serialization for runtime entity persistence
- Thread-safe repositories with ZLinq queries

### Scripting

- MoonSharp Lua runtime
- Attribute-based script modules (`[ScriptModule]`, `[ScriptFunction]`)
- Automatic `.luarc` generation for editor tooling
- Callback system for game events

## Performance Characteristics

Moongate focuses on predictable runtime behavior through:

- **Deterministic Scheduling** - Single game-loop processing and explicit cross-thread queues
- **Static Registration** - Source generators remove bootstrap discovery overhead
- **Explicit Persistence** - Snapshot and journal formats are typed and versioned
- **Operational Simplicity** - Standard .NET publish and container workflows

## Current Status

Moongate v2 is **actively in development**. The following features are implemented:

### Implemented

- [x] TCP server startup and connection lifecycle
- [x] Packet framing/parsing (fixed and variable sizes)
- [x] Attribute-based packet mapping with source generation
- [x] Inbound message bus (`IMessageBusService`)
- [x] Domain event bus with 43+ game event types
- [x] Outbound event listeners
- [x] Session split (transport vs gameplay context)
- [x] Lua scripting runtime with 16 script modules
- [x] Embedded HTTP server with OpenAPI/Scalar and JWT authentication
- [x] Snapshot + Journal persistence with MemoryPack serialization
- [x] Interactive console UI with 19 console commands
- [x] Timer wheel with metrics
- [x] Unit tests for core systems
- [x] Login handshake + character selection + character creation
- [x] Movement request validation with ACK/Deny flow
- [x] Item interaction baseline (pickup, drop, equip, double-click, single-click)
- [x] Target cursor request/response pipeline
- [x] General Information (`0xBF`) core subcommands
- [x] Sector-based world sync and lazy sector loading strategy
- [x] World generation system (decorations, doors, signs, teleporters, spawns)
- [x] NPC brain system with Lua coroutine-based AI
- [x] Gump system (builder API and file-based layouts)
- [x] Item script dispatcher with hook resolution
- [x] A* pathfinding service
- [x] Door system with toggle mechanics
- [x] Weather and lighting system
- [x] Visual effects system
- [x] REST API for user management, sessions, metrics, and item templates
- [x] React-based admin web UI
- [x] Source generators: 9 generators for compile-time registration
- [x] 24 file loaders for data asset management
- [x] 3-phase bootstrap system

### Planned

- [x] Combat v1 baseline (melee + ranged auto-attack, combatant state, warmode, timer-wheel scheduling, archery ammo/projectiles, PvE fame/karma awards)
- [ ] Skill system
- [ ] Item system completion (vendor/trade/economy semantics)
- [ ] House/shelter system
- [ ] Guild system
- [ ] Broader UO protocol listener coverage

## Who Is This For?

Moongate v2 is designed for:

- **Server Administrators** who want a performant, customizable UO server
- **Developers** interested in learning MMO server architecture
- **Contributors** who want to help build a modern UO server
- **Players** who want to run their own shards with unique features

## Getting Started

See the [Quick Start](quickstart.md) guide to get Moongate v2 running in minutes.

## Community & Support

- **GitHub**: https://github.com/moongate-community/moongate
- **Discord**: https://discord.gg/3HT7v95b
- **Docker Hub**: https://hub.docker.com/r/tgiachi/moongate

## License

Moongate v2 is licensed under the **GNU General Public License v3.0**.

---

**Next**: [Quick Start Guide](quickstart.md) - Get up and running in 5 minutes
