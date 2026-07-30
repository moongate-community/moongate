# 0xDD — Compressed Gump

<span class="mg-dir mg-dir-out">Server → Client</span>

Compressed gump (0xDD): the layout command string and the block of strings it indexes into, each zlib-deflated. <para> This is the only form modern servers send — ModernUO never writes the uncompressed 0xB0, and Moongate targets ClassicUO only. Each block is framed with its own compressed and uncompressed lengths, and collapses to a single zero when its payload is empty. Inside the strings block every entry is a big-endian character count followed by big-endian UTF-16, which is the opposite endianness to most of this protocol. </para>

- **Class:** [`CompressedGumpPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/CompressedGumpPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Serial` | `uint` |
| `TypeId` | `int` |
| `X` | `int` |
| `Y` | `int` |
| `Layout` | `string` |
| `Strings` | `IReadOnlyList<string>` |
