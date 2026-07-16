# 0x24 — Draw Container

<span class="mg-dir mg-dir-out">Server → Client</span>

Draw container (0x24): opens the container's gump on the client. 9 bytes — the modern client expects a trailing constant the older 7-byte form did not have. The contents follow in a separate container content (0x3C) packet.

- **Class:** [`DrawContainerPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/DrawContainerPacket.cs)
- **Size:** 7 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Container` | `Serial` |
| `GumpId` | `ushort` |
