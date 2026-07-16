# 0xBF/0x08 — Map Change

<span class="mg-dir mg-dir-out">Server → Client</span>

Map (facet) change (0xBF sub-command 0x08): switches the client to the given map. 6 bytes fixed.

- **Class:** [`MapChangePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MapChangePacket.cs)
- **Size:** 6 bytes (fixed)
- **Sub-command:** `0x08` of General Information (`0xBF`)

## Fields

| Field | Type |
|---|---|
| `Map` | `MapType` |
