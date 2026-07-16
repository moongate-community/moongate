# 0x25 — Add Item To Container

<span class="mg-dir mg-dir-out">Server → Client</span>

Add item to container (0x25): drops one item into an already-open container gump. 21 bytes — the modern client carries a grid-location byte the older 20-byte form did not. Use container content (0x3C) to send a whole container at once instead.

- **Class:** [`AddItemToContainerPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/AddItemToContainerPacket.cs)
- **Size:** —

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `ItemId` | `ushort` |
| `Amount` | `ushort` |
| `Position` | `Point2D` |
| `Container` | `Serial` |
| `Hue` | `Hue` |
