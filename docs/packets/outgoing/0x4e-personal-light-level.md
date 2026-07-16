# 0x4E — Personal Light Level

<span class="mg-dir mg-dir-out">Server → Client</span>

Personal light level (0x4E): the light radiating around the given mobile. 6 bytes fixed.

- **Class:** [`PersonalLightLevelPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/PersonalLightLevelPacket.cs)
- **Size:** 6 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Level` | `byte` |
