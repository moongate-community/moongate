# 0x3C — Container Content

<span class="mg-dir mg-dir-out">Server → Client</span>

Container content (0x3C): every item inside a container, in one variable-length packet. Sent right after draw container (0x24) to fill the gump that was just opened. Each entry repeats the container's serial, which is how the client knows where to draw it.

- **Class:** [`ContainerContentPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/ContainerContentPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Container` | `Serial` |
| `Items` | `IReadOnlyList<ContainerItem>` |
