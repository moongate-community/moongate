# 0x73 — Ping

<span class="mg-dir mg-dir-in">Client → Server</span>

Ping / keep-alive (0x73): the client sends this periodically with a rolling sequence byte and expects the server to echo it straight back, or it eventually drops the connection. 2 bytes fixed.

- **Class:** [`PingPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/PingPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Sequence` | `byte` |
