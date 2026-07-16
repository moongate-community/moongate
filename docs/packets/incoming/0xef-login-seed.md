# 0xEF — Login Seed

<span class="mg-dir mg-dir-in">Client → Server</span>

Login seed (0xEF): connection seed and client version, sent first by ClassicUO.

- **Class:** [`LoginSeedPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/LoginSeedPacket.cs)
- **Size:** 21 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Seed` | `uint` |
| `Major` | `uint` |
| `Minor` | `uint` |
| `Revision` | `uint` |
| `Prototype` | `uint` |
