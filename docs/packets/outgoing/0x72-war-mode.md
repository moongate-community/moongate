# 0x72 — War Mode

<span class="mg-dir mg-dir-out">Server → Client</span>

War mode (0x72): toggles the client's combat stance. 5 bytes fixed.

- **Class:** [`WarModePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/WarModePacket.cs)
- **Size:** 5 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `WarMode` | `bool` |
