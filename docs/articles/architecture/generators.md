# Source Generators

Moongate uses source generators to move registration and mapping work from runtime to compile-time.
This improves startup predictability, reduces reflection-heavy paths, and helps NativeAOT compatibility.

## Why We Use Generators

- Remove manual opcode/registration duplication.
- Keep runtime bootstrap deterministic.
- Reduce reflection-based discovery in hot startup paths.
- Keep AOT behavior stable by emitting explicit code.

## Current Generators

Moongate now uses a single generator project: `Moongate.Generators`.

It contains these generators:

### Packet Table Generator

- Input: packet classes decorated with `[PacketHandler(...)]`
- Output:
  - generated packet-table registration (`PacketTable.RegisterGenerated(...)`)
  - generated opcode constants (`PacketDefinition`)

### Packet Listener Registration Generator

- Input: listener classes decorated with `[RegisterPacketHandler(...)]`
- Output:
  - generated bootstrap listener wiring (`BootstrapPacketHandlerRegistration.RegisterGenerated(...)`)
  - compile-time mapping `opcode -> RegisterPacketHandler<TListener>(...)`

### Game Event Listener Registration Generator

- Input: listener classes decorated with `[RegisterGameEventListener]` and implementing one or more `IGameEventListener<TEvent>`
- Output:
  - generated bootstrap game-event subscription wiring (`BootstrapGameEventListenerRegistration.SubscribeGenerated(...)`)
  - compile-time mapping `listener -> RegisterListener<TEvent>(...)`

### Console Command Registration Generator

- Input: command classes decorated with `[RegisterConsoleCommand(...)]` and implementing `ICommandExecutor`
- Output:
  - generated DI singleton registrations (`BootstrapConsoleCommandRegistration.RegisterServicesGenerated(...)`)
  - generated command bindings (`BootstrapConsoleCommandRegistration.RegisterCommandsGenerated(...)`)
  - compile-time mapping `attribute metadata -> ICommandSystemService.RegisterCommand(...)`

### Metrics Mapper Generator

- Input: snapshot properties decorated with metric metadata
- Output:
  - generated metric mapper extensions used by collection/HTTP export

### Script Module Registration Generator

- Input: classes in `Moongate.Scripting` and `Moongate.Server` decorated with `[ScriptModule(...)]`
- Output:
  - generated `Moongate.Scripting.Generated.ScriptModuleRegistry.Register(...)`
  - compile-time registration of script modules in DryIoc

### Version Metadata Generator

- Input: `Moongate.Server` project properties (`Version`, `Codename`)
- Output:
  - generated `Moongate.Server.Data.Version.VersionUtils`
  - strongly-typed version/codename values for runtime bootstrap usage

## Runtime Flow

1. Build compiles generators and emits generated C# files.
2. Runtime projects consume generated artifacts through analyzer references.
3. At runtime:
   - packet descriptors come from generated packet table registration.
   - listener wiring comes from generated bootstrap registration.
   - game-event listeners are subscribed from generated bootstrap registration.
   - command executors are registered and bound from generated console command registration.
   - metric snapshots are mapped through generated mappers.
   - script modules are registered through generated script module registry.
   - version/codename are read from generated `VersionUtils`.

## Notes

- Generators are implementation tools and are excluded from DocFX API metadata output.
- Public runtime APIs remain documented under normal module namespaces.

---

**Previous**: [Solution Structure](solution.md) | **Next**: [Network System](network.md)
