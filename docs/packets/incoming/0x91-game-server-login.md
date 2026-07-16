# 0x91 — Game Server Login

<span class="mg-dir mg-dir-in">Client → Server</span>

Game server login (0x91): the auth key from the redirect plus the account credentials.

- **Class:** [`GameServerLoginPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/GameServerLoginPacket.cs)
- **Size:** 65 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `AuthKey` | `uint` |
| `Account` | `string` |
| `Password` | `string` |
