# 0x07 — Pick Up Item

<span class="mg-dir mg-dir-in">Client → Server</span>

Pick up item (0x07): the client asks to lift an item onto its cursor. `Amount` is how much of a stack to take, and is clamped server-side to what the stack actually holds. 7 bytes fixed.

- **Class:** [`PickUpItemPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/PickUpItemPacket.cs)
- **Size:** 7 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Amount` | `ushort` |
