# 0xDC — Opl Info

<span class="mg-dir mg-dir-out">Server → Client</span>

OPL info (0xDC): tells the client the current property-list revision of an object. 9 bytes fixed. The client compares the hash with its cache and requests the full list (0xD6) when it differs. The wire value is the raw content hash flagged with 0x40000000.

- **Class:** [`OplInfoPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/OplInfoPacket.cs)
- **Size:** 9 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Hash` | `int` |
