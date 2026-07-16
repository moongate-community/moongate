# 0xBF — Stat Lock Info

<span class="mg-dir mg-dir-out">Server → Client</span>

Stat lock info (0xBF sub-command 0x19): the up/down/lock state of the three stats, packed two bits each into a single byte. 12 bytes fixed. Without it the client's status-gump arrows never learn the server's state, so they reset on every login.

- **Class:** [`StatLockInfoPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/StatLockInfoPacket.cs)
- **Size:** 12 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `StrengthLock` | `StatLockType` |
| `DexterityLock` | `StatLockType` |
| `IntelligenceLock` | `StatLockType` |
