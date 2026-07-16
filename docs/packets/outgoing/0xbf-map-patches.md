# 0xBF/0x18 — Map Patches

<span class="mg-dir mg-dir-out">Server → Client</span>

Map patches (0xBF sub-command 0x18): declares the static/land map-diff block counts for the four classic facets. Moongate ships no diff files, so every count is zero and the client uses the base maps. 41 bytes fixed.

- **Class:** [`MapPatchesPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MapPatchesPacket.cs)
- **Size:** 41 bytes (fixed)
- **Sub-command:** `0x18` of General Information (`0xBF`)

## Fields

This packet carries no fields beyond its opcode.
