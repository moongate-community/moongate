# 0xF8 — Character Creation

<span class="mg-dir mg-dir-in">Client → Server</span>

Character creation (0xF8): the new 106-byte creation packet sent by clients 7.0.16.0 and later. This reads the wire fields and decodes gender/race; resolving profession, city and applying the starting loadout is the handler's job. An unrecognized gender/race byte falls back to a male human.

- **Class:** [`CharacterCreationPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/CharacterCreationPacket.cs)
- **Size:** 106 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Slot` | `int` |
| `Name` | `string` |
| `ClientFlags` | `uint` |
| `ProfessionId` | `byte` |
| `Gender` | `GenderType` |
| `Race` | `RaceType` |
| `Strength` | `byte` |
| `Dexterity` | `byte` |
| `Intelligence` | `byte` |
| `Skills` | `IReadOnlyList<CharacterSkill>` |
| `SkinHue` | `short` |
| `HairStyle` | `short` |
| `HairHue` | `short` |
| `FacialHairStyle` | `short` |
| `FacialHairHue` | `short` |
| `StartingCityIndex` | `short` |
| `ShirtHue` | `short` |
| `PantsHue` | `short` |
