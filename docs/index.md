# 🌙 Moongate v2 Documentation

Welcome to the official documentation for **Moongate v2**, a modern Ultima Online server emulator built with .NET 10 and NativeAOT compilation.

> **Moongate v2** is designed for performance, modularity, and extensibility. It leverages modern .NET features to deliver an ultra-fast, customizable UO server experience.

## 📚 Documentation Sections

### Getting Started

New to Moongate v2? Start here:

- [Introduction](articles/getting-started/introduction.md) - What is Moongate v2?
- [Quick Start](articles/getting-started/quickstart.md) - Get up and running in minutes
- [Installation](articles/getting-started/installation.md) - Detailed installation guide
- [Configuration](articles/getting-started/configuration.md) - Configure your server

### Architecture

Understand the internal workings:

- [Architecture Overview](articles/architecture/overview.md) - High-level system architecture
- [Network System](articles/architecture/network.md) - TCP server and packet handling
- [Game Loop](articles/architecture/game-loop.md) - Timestamp-driven game loop scheduling
- [Event System](articles/architecture/events.md) - Domain events and message bus
- [Session Management](articles/architecture/sessions.md) - Client session handling
- [Source Generators](articles/architecture/generators.md) - Compile-time registration and mapping

### Scripting

Extend your server with Lua:

- [Scripting Overview](articles/scripting/overview.md) - Lua scripting introduction
- [Script Modules](articles/scripting/modules.md) - Create custom script modules
- [API Reference](articles/scripting/api.md) - Scripting API documentation

### Persistence

Data storage and retrieval:

- [Persistence Overview](articles/persistence/overview.md) - Snapshot + Journal model
- [Data Format](articles/persistence/format.md) - Binary serialization details
- [Repositories](articles/persistence/repositories.md) - Query and data access

### Networking

Deep dive into network protocols:

- [Packet System](articles/networking/packets.md) - Packet registration and handling
- [Protocol Reference](articles/networking/protocol.md) - UO protocol implementation

### Development

- [Code Convention](CODE_CONVENTION.md) - Project-wide coding and test structure conventions

### Operations

- [Stress Test](articles/operations/stress-test.md) - Black-box UO socket load testing (100 clients scenario)

## 🔗 Quick Links

- [GitHub Repository](https://github.com/moongate-community/moongatev2)
- [API Reference](api/toc.yml) - Full .NET API documentation
- [Docker Hub](https://hub.docker.com/r/tgiachi/moongate) - Pre-built Docker images
- [Discord Community](https://discord.gg/3HT7v95b) - Join our community

## 📦 Key Features

| Feature | Description |
|---------|-------------|
| **.NET 10** | Built on the latest .NET runtime for maximum performance |
| **NativeAOT** | Ahead-of-Time compilation for faster startup and lower memory |
| **Lua Scripting** | Extensible gameplay via MoonSharp Lua engine |
| **Event-Driven** | Clean separation between network parsing and domain logic |
| **File Persistence** | Lightweight snapshot + journal storage model |
| **HTTP Metrics** | Built-in Prometheus metrics on `:8088/metrics` |
| **Docker Ready** | Official Docker images with NativeAOT binaries |

## 🚀 Current Status

Moongate v2 is **actively in development**. Current capabilities include:

- ✅ TCP server with connection lifecycle management
- ✅ Packet framing/parsing for fixed and variable sizes
- ✅ Source-generated packet registration
- ✅ Source-generated packet listener bootstrap registration
- ✅ Source-generated metrics snapshot mapping
- ✅ Inbound message bus for network → game-loop communication
- ✅ Domain event bus with player connect/disconnect events
- ✅ Lua scripting runtime with `.luarc` generation
- ✅ Embedded HTTP server with OpenAPI/Scalar documentation
- ✅ Snapshot + Journal persistence system
- ✅ Interactive console UI with colored logging
- ✅ Command system with source-generated registration (`ICommandExecutor` + `[RegisterConsoleCommand]`)
- ✅ Command authorization by source + `AccountType`
- ✅ Console `Tab` / `Shift+Tab` autocomplete (commands + argument providers)
- ✅ Contextual `help <command>` output
- ✅ Custom DocFX Moongate theme with moon branding and Fira Code
- ✅ Timer wheel metrics integration

## 📄 License

This project is licensed under the **GNU General Public License v3.0**. See the [LICENSE](https://github.com/moongate-community/moongatev2/blob/main/LICENSE) file for details.

---

**Built for Speed. Designed for Community. Powered by Innovation.**

*Moongate v2 - Where classic meets cutting-edge*
