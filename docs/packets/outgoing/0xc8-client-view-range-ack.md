# 0xC8 — Client View Range Ack

<span class="mg-dir mg-dir-out">Server → Client</span>

Client view range acknowledgement (0xC8): reports the view range the server actually granted, so a client that asked for more than the maximum learns what it got. 2 bytes fixed.

- **Class:** [`ClientViewRangeAckPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/ClientViewRangeAckPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Range` | `byte` |
