# 0x20 — Draw Game Player

<span class="mg-dir mg-dir-out">Server → Client</span>

Draw game player / mobile update (0x20): positions and renders the player's own mobile. 19 bytes fixed.

- **Class:** [`MobileUpdatePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MobileUpdatePacket.cs)
- **Size:** 19 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Body` | `ushort` |
| `Hue` | `Hue` |
| `Flags` | `byte` |
| `X` | `ushort` |
| `Y` | `ushort` |
| `Z` | `sbyte` |
| `Direction` | `DirectionType` |
