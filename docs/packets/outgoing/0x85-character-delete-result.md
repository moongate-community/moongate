# 0x85 — Character Delete Result

<span class="mg-dir mg-dir-out">Server → Client</span>

Character delete result (0x85): why a deletion was refused. 2 bytes fixed. Only sent on refusal — a successful deletion is reported by sending the updated character list instead.

- **Class:** [`CharacterDeleteResultPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/CharacterDeleteResultPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Reason` | `DeleteResultType` |
