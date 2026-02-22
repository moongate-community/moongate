# Source Generators

Moongate uses source generators to move registration and mapping work from runtime to compile-time.
This improves startup predictability, reduces reflection-heavy paths, and helps NativeAOT compatibility.

## Why We Use Generators

- Remove manual opcode/registration duplication.
- Keep runtime bootstrap deterministic.
- Reduce reflection-based discovery in hot startup paths.
- Keep AOT behavior stable by emitting explicit code.

## Current Generators

### `Moongate.Network.Packets.Generators`

Purpose:

- Generates packet registry/table wiring from packet metadata attributes.
- Produces packet opcode constants (`PacketDefinition`) used by server handlers.

Input:

- packet classes decorated with `[PacketHandler(...)]`.

Output:

- generated registration code for packet metadata.
- generated opcode constants consumed by server code.

### `Moongate.Server.PacketHandlers.Generators`

Purpose:

- Generates server packet listener bootstrap registration.

Input:

- listener classes decorated with `[RegisterPacketHandler(opCode)]`.
- supports multiple attributes per listener class.

Output:

- generated implementation for `BootstrapPacketHandlerRegistration.RegisterGenerated(...)`.
- compile-time mapping:
  - `opcode -> listener registration call`
  - no manual hardcoded bootstrap list required.

### `Moongate.Server.Metrics.Generators`

Purpose:

- Generates metric snapshot mapping code from metric-decorated snapshot models.

Input:

- snapshot models/properties marked for metrics export.

Output:

- generated mapper extensions used by HTTP metrics/export paths.

## Runtime Flow

1. Build compiles generators and emits generated C# files.
2. `Moongate.Server` consumes generated artifacts through analyzer references.
3. At runtime:
   - packet descriptors come from generated packet table registration.
   - listener wiring comes from generated bootstrap registration.
   - metric snapshots are mapped through generated mappers.

## Notes

- Generators are implementation tools and are excluded from DocFX API metadata output.
- Public runtime APIs remain documented under normal module namespaces.

---

**Previous**: [Solution Structure](solution.md) | **Next**: [Network System](network.md)
