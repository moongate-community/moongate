# 0x5B — Game Time

<span class="mg-dir mg-dir-out">Server → Client</span>

Game time (0x5B): the in-world clock shown to the client. 4 bytes fixed.

- **Class:** [`GameTimePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/GameTimePacket.cs)
- **Size:** 4 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Hour` | `byte` |
| `Minute` | `byte` |
| `Second` | `byte` |
