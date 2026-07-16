# 0x1D — Delete Object

<span class="mg-dir mg-dir-out">Server → Client</span>

Delete object (0x1D): the entity is gone — stop drawing it. 5 bytes fixed. Sent to everyone who can see a mobile or item that was removed, deleted or moved out of view.

- **Class:** [`DeleteObjectPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/DeleteObjectPacket.cs)
- **Size:** 5 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
