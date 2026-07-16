# 0x83 — Delete Character

<span class="mg-dir mg-dir-in">Client → Server</span>

Delete character (0x83): the client asks to delete the character in the given slot. 39 bytes fixed. The password field is a leftover from the days the client sent it here and is ignored.

- **Class:** [`DeleteCharacterPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/DeleteCharacterPacket.cs)
- **Size:** 39 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Slot` | `int` |
