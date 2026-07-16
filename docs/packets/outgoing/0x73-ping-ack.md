# 0x73 — Ping Ack

<span class="mg-dir mg-dir-out">Server → Client</span>

Ping acknowledgement (0x73): echoes the client's keep-alive sequence byte straight back. 2 bytes fixed.

- **Class:** [`PingAckPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/PingAckPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Sequence` | `byte` |
