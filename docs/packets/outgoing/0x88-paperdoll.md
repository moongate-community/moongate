# 0x88 — Paperdoll

<span class="mg-dir mg-dir-out">Server → Client</span>

Open paperdoll (0x88): tells the client to open the character window for a mobile. 66 bytes fixed. The text is the label shown on the window; `CanLift` lets the viewer drag items off the paperdoll, which is why it is only set for your own character.

- **Class:** [`PaperdollPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/PaperdollPacket.cs)
- **Size:** 66 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Text` | `string` |
| `Warmode` | `bool` |
| `CanLift` | `bool` |
