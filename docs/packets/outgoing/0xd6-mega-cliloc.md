# 0xD6 — Mega Cliloc

<span class="mg-dir mg-dir-out">Server → Client</span>

Mega cliloc (0xD6): the property list ("tooltip") of one object — cliloc lines with UTF-16LE arguments, preceded by the content hash the client caches against. Variable length. The hash travels raw here; the 0xDC notification carries the same value flagged with 0x40000000.

- **Class:** [`MegaClilocPacket`](https://github.com/moongate-community/moongate/blob/main/src/Moongate.Network/Packets/Outgoing/MegaClilocPacket.cs)
- **Size:** Variable

## Fields

| Field | Type |
|---|---|
| `Serial` | `Serial` |
| `Hash` | `int` |
| `Entries` | `IReadOnlyList<OplEntry>` |
