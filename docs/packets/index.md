# Ultima Online Packets

Moongate speaks the classic Ultima Online wire protocol. Every message is a
*packet*: a byte stream that starts with a one-byte **opcode** and is decoded
into (or encoded from) a small C# `record struct` — incoming packets implement
`IIncomingPacket<T>`, outgoing ones `IOutgoingPacket`. This page maps the
protocol surface currently implemented for the ClassicUO **7.x** client target.

[!include[](includes/packet-table.md)]

## How the protocol works

- **Opcodes.** The first byte of every packet identifies it. The same opcode can
  mean different things per direction (for example `0x73` is a ping from the
  client and a ping acknowledgement from the server).
- **Fixed vs variable length.** Most packets have a fixed size known from the
  opcode. Variable-length packets carry their total length as a big-endian
  `ushort` right after the opcode.
- **Login vs game phase.** The client first talks to the *login server*
  (account auth, shard list, server select), receives a redirect (`0x8C`), then
  reconnects to the *game server* and enters the world.
- **The `0xBF` multiplexer.** General Information (`0xBF`) is a container: a
  `ushort` sub-command selects the actual meaning (map patches, stat locks,
  and many more). Moongate models each implemented sub-command as its own
  outgoing packet class.

## How a packet is implemented

Each packet is a self-contained `readonly record struct` that knows how to read
or write itself:

```csharp
/// <summary>
/// Double click (0x06): the client double-clicked an entity, identified by its serial. 5 bytes fixed.
/// Whether the target is a mobile or an item is decided later from the serial's range.
/// </summary>
public readonly record struct DoubleClickPacket(Serial Target) : IIncomingPacket<DoubleClickPacket>
{
    public static byte PacketId => 0x06;

    public static DoubleClickPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id
        var target = new Serial(reader.ReadUInt32());

        return new(target);
    }
}
```

The reference pages in this section are generated from those classes by
`scripts/generate-packet-docs.cs` — run `dotnet run scripts/generate-packet-docs.cs`
from the repository root after adding or changing a packet.

## Packet families

<div class="mg-cards mg-cards-article">
  <div class="mg-card">
    <h3>Login &amp; shard select</h3>
    <div class="mg-card-ops">0xEF · 0x80 · 0x82 · 0xA8 · 0xA0 · 0x8C · 0x91 · 0xBD</div>
    <p>Seed, account auth, server list, shard select, game-server handoff.</p>
  </div>
  <div class="mg-card">
    <h3>Characters</h3>
    <div class="mg-card-ops">0xA9 · 0xF8 · 0x5D · 0x83 · 0x85 · 0x86</div>
    <p>Character list, creation, selection, deletion and list updates.</p>
  </div>
  <div class="mg-card">
    <h3>Enter world</h3>
    <div class="mg-card-ops">0x1B · 0xB9 · 0x55 · 0x4E · 0x4F · 0x5B · 0xBC</div>
    <p>Login confirm, feature flags, light levels, game time, season.</p>
  </div>
  <div class="mg-card">
    <h3>Movement</h3>
    <div class="mg-card-ops">0x02 · 0x22 · 0x20 · 0x78</div>
    <p>Move requests, acks, and mobile position updates.</p>
  </div>
  <div class="mg-card">
    <h3>Status &amp; skills</h3>
    <div class="mg-card-ops">0x11 · 0x88 · 0x3A · 0x72 · 0xBF</div>
    <p>Mobile status, paperdoll, skills, war mode, stat/skill locks.</p>
  </div>
  <div class="mg-card">
    <h3>Interaction &amp; keepalive</h3>
    <div class="mg-card-ops">0x06 · 0x09 · 0x73</div>
    <p>Single/double click on entities and ping round-trips.</p>
  </div>
</div>
