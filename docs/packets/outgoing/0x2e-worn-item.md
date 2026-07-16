# 0x2E — Worn Item

<span class="mg-dir mg-dir-out">Server → Client</span>

Worn item (0x2E): draws a single item on a mobile that the client already knows about. 15 bytes fixed. Equipment sent at the moment a mobile first appears rides inside mobile incoming (0x78) instead; this is for equipping and re-hueing afterwards.

- **Class:** [`WornItemPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/WornItemPacket.cs)
- **Size:** 15 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `ItemId` | `ushort` |
| `Layer` | `LayerType` |
| `Mobile` | `Serial` |
| `Hue` | `Hue` |
