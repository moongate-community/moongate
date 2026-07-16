# 0x4F — Overall Light Level

<span class="mg-dir mg-dir-out">Server → Client</span>

Overall (world) light level (0x4F): 0 is full daylight, higher is darker. 2 bytes fixed.

- **Class:** [`OverallLightLevelPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/OverallLightLevelPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Level` | `byte` |
