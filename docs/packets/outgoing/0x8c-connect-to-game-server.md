# 0x8C — Connect To Game Server

<span class="mg-dir mg-dir-out">Server → Client</span>

Connect to game server (0x8C): redirects the client to the game port with an auth key.

- **Class:** [`ConnectToGameServerPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/ConnectToGameServerPacket.cs)
- **Size:** 11 bytes (fixed)

## Fields

| Field | Type |
|---|---|
| `Address` | `IPAddress` |
| `Port` | `ushort` |
| `AuthKey` | `uint` |
