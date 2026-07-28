# 0xC8 — Client View Range

<span class="mg-dir mg-dir-in">Client → Server</span>

Client view range (0xC8): the client announces the update range it wants, in tiles, whenever the option changes. The server clamps it and echoes the accepted value back with the same opcode. 2 bytes fixed.

- **Class:** [`ClientViewRangePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/ClientViewRangePacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Range` | `byte` |
