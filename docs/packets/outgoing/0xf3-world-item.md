# 0xF3 — World Item

<span class="mg-dir mg-dir-out">Server → Client</span>

World item (0xF3): draws an item lying in the world. 26 bytes — the modern client's form, which adds a trailing short to the 24-byte one. This packet can carry mobiles and multis too; we only send items, so the entity type is fixed.

- **Class:** [`WorldItemPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/WorldItemPacket.cs)
- **Size:** —

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `ItemId` | `ushort` |
| `Amount` | `ushort` |
| `Position` | `Point3D` |
| `Hue` | `Hue` |
