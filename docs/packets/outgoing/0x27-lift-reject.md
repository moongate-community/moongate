# 0x27 — Lift Reject

<span class="mg-dir mg-dir-out">Server → Client</span>

Reject move item request (0x27): the lift the client asked for is refused, and why. 2 bytes fixed. There is no matching "lift approved" — a successful lift is confirmed by the packets that follow it.

- **Class:** [`LiftRejectPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/LiftRejectPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Reason` | `LiftRejectReasonType` |
