# 0x22 — Movement Ack

<span class="mg-dir mg-dir-out">Server → Client</span>

Movement ack (0x22): confirms the step with the client's sequence number.

- **Class:** [`MovementAckPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MovementAckPacket.cs)
- **Size:** 3 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Sequence` | `byte` |
| `Notoriety` | `NotorietyType` |
