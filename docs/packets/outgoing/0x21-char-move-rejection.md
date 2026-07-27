# 0x21 — Char Move Rejection

<span class="mg-dir mg-dir-out">Server → Client</span>

Move rejected (0x21): echoes the rejected sequence with the mover's true position, forcing a client snap-back.

- **Class:** [`MoveRejectPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MoveRejectPacket.cs)
- **Size:** 8 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Sequence` | `byte` |
| `X` | `ushort` |
| `Y` | `ushort` |
| `Direction` | `DirectionType` |
| `Z` | `sbyte` |
