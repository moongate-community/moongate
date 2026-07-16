# 0x5D — Character Select

<span class="mg-dir mg-dir-in">Client → Server</span>

Play character / character login (0x5D): the client picks an existing character slot to enter the world with. 73 bytes fixed. Only the character name and the chosen slot are meaningful to us.

- **Class:** [`CharacterSelectPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/CharacterSelectPacket.cs)
- **Size:** 73 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Name` | `string` |
| `Slot` | `int` |
