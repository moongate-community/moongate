# 0x86 — Character List Update

<span class="mg-dir mg-dir-out">Server → Client</span>

Character list update (0x86): the account's character list after it changed, so the client can redraw the selection screen. Length is `4 + 60*SlotCount`. Unlike the login character list (0xA9) it carries no starting cities.

- **Class:** [`CharacterListUpdatePacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/CharacterListUpdatePacket.cs)
- **Size:** —

## Fields

| Field | Type |
|---|---|
| `Characters` | `IReadOnlyList<string>` |
| `SlotCount` | `byte` |
