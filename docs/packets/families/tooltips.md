# Tooltips

Object property lists: the 0xD6 request/response pair and the 0xDC revision notification.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0xD6`](../incoming/0xd6-mega-cliloc-request.md) | Mega Cliloc Request | C → S | Variable | The client asks for the property lists of a batch of objects, identified by serial. |
| [`0xD6`](../outgoing/0xd6-mega-cliloc.md) | Mega Cliloc | S → C | Variable | The property list ("tooltip") of one object — cliloc lines with UTF-16LE arguments, preceded by the content hash the client caches against. |
| [`0xDC`](../outgoing/0xdc-opl-info.md) | Opl Info | S → C | 9 bytes (fixed) | Tells the client the current property-list revision of an object. |
