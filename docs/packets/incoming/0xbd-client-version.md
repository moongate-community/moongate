# 0xBD — Client Version

<span class="mg-dir mg-dir-in">Client → Server</span>

Client version (0xBD): the client answers the server's version request with its build string (e.g. "7.0.115.0"), null-terminated ASCII. Variable length: 3-byte header + string.

- **Class:** [`ClientVersionPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/ClientVersionPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Version` | `string` |
