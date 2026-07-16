# 0x06 — Double Click

<span class="mg-dir mg-dir-in">Client → Server</span>

Double click (0x06): the client double-clicked an entity, identified by its serial. 5 bytes fixed. Whether the target is a mobile or an item is decided later from the serial's range.

- **Class:** [`DoubleClickPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/DoubleClickPacket.cs)
- **Size:** 5 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Target` | `Serial` |
