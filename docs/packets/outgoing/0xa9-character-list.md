# 0xA9 — Character List

<span class="mg-dir mg-dir-out">Server → Client</span>

Character list (0xA9): the character slots followed by the starting cities, in the extended 7.0.13+ layout. The first `Characters` fill the slots by index; the rest stay empty. Length is `11 + 60*SlotCount + 89*Cities.Count`.

- **Class:** [`CharacterListPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/CharacterListPacket.cs)
- **Size:** —

## Fields

| Field | Type |
|---|---|
| `Characters` | `IReadOnlyList<string>` |
| `Cities` | `IReadOnlyList<StartingCity>` |
| `SlotCount` | `byte` |
| `Flags` | `CharacterListFlagType` |
