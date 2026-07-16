# 0x02 — Move Request

<span class="mg-dir mg-dir-in">Client → Server</span>

Move request (0x02): one step or turn, with anti-fastwalk key.

- **Class:** [`MoveRequestPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/MoveRequestPacket.cs)
- **Size:** —

## Fields

| Field | Type |
|---|---|
| `Direction` | `DirectionType` |
| `Sequence` | `byte` |
| `FastwalkKey` | `uint` |
