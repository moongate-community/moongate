# 0x82 — Login Denied

<span class="mg-dir mg-dir-out">Server → Client</span>

Login denied (0x82): rejects the login with a protocol reason code.

- **Class:** [`LoginDeniedPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/LoginDeniedPacket.cs)
- **Size:** 2 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Reason` | `LoginDeniedReasonType` |
