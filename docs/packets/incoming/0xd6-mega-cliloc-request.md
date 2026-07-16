# 0xD6 — Mega Cliloc Request

<span class="mg-dir mg-dir-in">Client → Server</span>

Mega cliloc request (0xD6): the client asks for the property lists of a batch of objects, identified by serial. Variable length: a 3-byte header followed by 4-byte serials; a payload that is not a multiple of four yields an empty batch.

- **Class:** [`MegaClilocRequestPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Incoming/MegaClilocRequestPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Serials` | `IReadOnlyList<Serial>` |
