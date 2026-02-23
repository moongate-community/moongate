# Packet System

Moongate v2 uses concrete packet classes implementing `IGameNetworkPacket`.

## Packet Contract

```csharp
public interface IGameNetworkPacket
{
    byte OpCode { get; }
    int Length { get; } // -1 for variable-length
    bool TryParse(ReadOnlySpan<byte> data);
    void Write(ref SpanWriter writer);
}
```

## Registration Model

Packets are decorated with:

```csharp
[PacketHandler(0x02, PacketSizing.Fixed, Length = 7, Description = "Move Request")]
```

Key types:

- `PacketHandlerAttribute`
  - `OpCode`, `Sizing`, `Length`, `Description`
- `PacketSizing`
  - `Fixed`, `Variable`
- `PacketRegistry`
  - metadata + packet factory per opcode
- `PacketTable.Register(PacketRegistry)`
  - generated registration entrypoint

## Runtime Use

`NetworkService` uses `PacketRegistry` to:

1. resolve descriptor (`TryGetDescriptor`)
2. create packet instance (`TryCreatePacket`)
3. call `TryParse(rawPacket)`
4. publish `IncomingGamePacket` to message bus

`PacketDispatchService` then routes by opcode to registered `IPacketListener` instances.

## Serialization

Outbound packets are serialized with `SpanWriter` in `OutboundPacketSender`.

- fixed packets typically pre-size buffer with `Length`
- variable packets can use dynamic writer growth
- packet logging can output hex dump when `LogPacketData` is enabled

## Source Generation

`Moongate.Generators` provides generated packet artifacts used by runtime:

- packet table registration code
- packet opcode constants (`PacketDefinition` partial)

This avoids manual opcode duplication and keeps registration centralized.

## Listener Registration Generation

Server listener wiring is also source-generated.

Listener classes declare handled opcodes:

```csharp
[RegisterPacketHandler(PacketDefinition.MoveRequestPacket)]
public class MovementHandler : BasePacketListener
{
    // ...
}
```

`Moongate.Generators` produces bootstrap code that calls
`RegisterPacketHandler<TListener>(container, opCode)` for all discovered attributes.

This keeps:

- listener mapping explicit and compile-time validated.
- bootstrap code shorter and easier to maintain.
- runtime startup free from reflection-based handler scanning.

---

**Previous**: [Protocol Reference](protocol.md)
