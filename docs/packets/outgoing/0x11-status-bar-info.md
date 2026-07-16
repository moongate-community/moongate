# 0x11 — Status Bar Info

<span class="mg-dir mg-dir-out">Server → Client</span>

Status bar info (0x11): the player's own status window. Written in the High Seas layout (version 6, 121 bytes) that modern 7.x clients expect. Combat-derived figures Moongate does not model yet (resists, luck, weapon damage, tithing, gold, weight) are sent as zero. Back-patched.

- **Class:** [`MobileStatusPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MobileStatusPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Name` | `string` |
| `Hits` | `ushort` |
| `HitsMax` | `ushort` |
| `Female` | `bool` |
| `Strength` | `ushort` |
| `Dexterity` | `ushort` |
| `Intelligence` | `ushort` |
| `Stamina` | `ushort` |
| `StaminaMax` | `ushort` |
| `Mana` | `ushort` |
| `ManaMax` | `ushort` |
| `Race` | `RaceType` |
| `StatCap` | `ushort` |
| `Followers` | `byte` |
| `FollowersMax` | `byte` |
