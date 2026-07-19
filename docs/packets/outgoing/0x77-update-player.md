# 0x77 — Update Player

<span class="mg-dir mg-dir-out">Server → Client</span>

Update player (0x77): broadcasts another mobile's position/facing to nearby players.

- **Class:** [`UpdatePlayerPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/UpdatePlayerPacket.cs)
- **Size:** 17 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Body` | `ushort` |
| `X` | `ushort` |
| `Y` | `ushort` |
| `Z` | `sbyte` |
| `Direction` | `DirectionType` |
| `Hue` | `Hue` |
| `Flags` | `byte` |
| `Notoriety` | `NotorietyType` |
