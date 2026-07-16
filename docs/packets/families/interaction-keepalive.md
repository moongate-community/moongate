# Interaction & keepalive

Single/double click, the 0xBF request multiplexer, and ping round-trips.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x06`](../incoming/0x06-double-click.md) | Double Click | C → S | 5 bytes (fixed) | The client double-clicked an entity, identified by its serial. |
| [`0x09`](../incoming/0x09-single-click.md) | Single Click | C → S | 5 bytes (fixed) | The client clicked an entity, identified by its serial. |
| [`0x73`](../incoming/0x73-ping.md) | Ping | C → S | 2 bytes (fixed) | The client sends this periodically with a rolling sequence byte and expects the server to echo it straight back, or it eventually drops the connection. |
| [`0x73`](../outgoing/0x73-ping-ack.md) | Ping Ack | S → C | 2 bytes (fixed) | Echoes the client's keep-alive sequence byte straight back. |
| [`0xBF`](../incoming/0xbf-general-information.md) | General Information | C → S | Variable | A multiplexed request whose meaning is chosen by a leading `SubCommand` (ushort). |
