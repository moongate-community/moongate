# 0xBC — Season Change

<span class="mg-dir mg-dir-out">Server → Client</span>

Seasonal information (0xBC): sets the client's season and optionally plays the season-change sound. 3 bytes fixed.

- **Class:** [`SeasonChangePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/SeasonChangePacket.cs)
- **Size:** 3 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Season` | `SeasonType` |
| `PlaySound` | `bool` |
