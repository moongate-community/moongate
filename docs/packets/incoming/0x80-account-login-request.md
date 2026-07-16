# 0x80 — Account Login Request

<span class="mg-dir mg-dir-in">Client → Server</span>

Account login request (0x80): credentials for the login server.

- **Class:** [`AccountLoginRequestPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/AccountLoginRequestPacket.cs)
- **Size:** 62 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Account` | `string` |
| `Password` | `string` |
| `NextLoginKey` | `byte` |
