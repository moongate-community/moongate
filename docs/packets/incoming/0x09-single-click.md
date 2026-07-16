# 0x09 — Single Click

<span class="mg-dir mg-dir-in">Client → Server</span>

Single click (0x09): the client clicked an entity, identified by its serial. 5 bytes fixed. Whether the target is a mobile or an item is decided later from the serial's range.

- **Class:** [`SingleClickPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/SingleClickPacket.cs)
- **Size:** 5 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Target` | `Serial` |
