# 0x78 — Mobile Incoming

<span class="mg-dir mg-dir-out">Server → Client</span>

Mobile incoming / draw object (0x78): draws a mobile and its equipped items on the client. Uses the modern layout (full 16-bit item ids, hue always written). Variable length.

- **Class:** [`MobileIncomingPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MobileIncomingPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Body` | `ushort` |
| `X` | `ushort` |
| `Y` | `ushort` |
| `Z` | `sbyte` |
| `Direction` | `DirectionType` |
| `Hue` | `Hue` |
| `Flags` | `byte` |
| `Notoriety` | `NotorietyType` |
| `Items` | `IReadOnlyList<MobileIncomingItem>` |
