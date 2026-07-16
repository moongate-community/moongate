# 0x1B — Login Confirm

<span class="mg-dir mg-dir-out">Server → Client</span>

Login confirmation / character locale and body (0x1B): the first packet of the enter-world burst. Tells the client which mobile it is playing, where it stands, and the facet's dimensions. 37 bytes fixed.

- **Class:** [`LoginConfirmPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/LoginConfirmPacket.cs)
- **Size:** 37 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Body` | `ushort` |
| `X` | `ushort` |
| `Y` | `ushort` |
| `Z` | `short` |
| `Direction` | `DirectionType` |
| `MapWidth` | `ushort` |
| `MapHeight` | `ushort` |
