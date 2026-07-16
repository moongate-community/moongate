# 0xA8 — Server List

<span class="mg-dir mg-dir-out">Server → Client</span>

Server list (0xA8): advertises the available shards. Card 09 sends the single shard.

- **Class:** [`ServerListPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/ServerListPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `ShardName` | `string` |
| `Address` | `IPAddress` |
