# 0x08 — Drop Item

<span class="mg-dir mg-dir-in">Client → Server</span>

Drop item (0x08): where the client wants to put the item it is holding. A `Container` of 0xFFFFFFFF means the ground at X/Y/Z; anything else is a container, and then X/Y are the position inside its gump. 15 bytes fixed.

- **Class:** [`DropItemPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/DropItemPacket.cs)
- **Size:** 15 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `X` | `ushort` |
| `Y` | `ushort` |
| `Z` | `sbyte` |
| `GridIndex` | `byte` |
| `Container` | `Serial` |
