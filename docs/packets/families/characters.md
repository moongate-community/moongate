# Characters

Character list, creation, selection, deletion and list updates.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x5D`](../incoming/0x5d-character-select.md) | Character Select | C → S | 73 bytes (fixed) | The client picks an existing character slot to enter the world with. |
| [`0x83`](../incoming/0x83-delete-character.md) | Delete Character | C → S | 39 bytes (fixed) | The client asks to delete the character in the given slot. |
| [`0x85`](../outgoing/0x85-character-delete-result.md) | Character Delete Result | S → C | 2 bytes (fixed) | Why a deletion was refused. |
| [`0x86`](../outgoing/0x86-character-list-update.md) | Character List Update | S → C | 304 bytes (fixed) | The account's character list after it changed, so the client can redraw the selection screen. |
| [`0xA9`](../outgoing/0xa9-character-list.md) | Character List | S → C | Variable | The character slots followed by the starting cities, in the extended 7.0.13+ layout. |
| [`0xF8`](../incoming/0xf8-character-creation.md) | Character Creation | C → S | 106 bytes (fixed) | The new 106-byte creation packet sent by clients 7.0.16.0 and later. |
