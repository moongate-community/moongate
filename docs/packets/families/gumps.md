# Gumps

Server-drawn dialogs: the compressed gump and the response naming the button pressed.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0xB1`](../incoming/0xb1-gump-menu-selection.md) | Gump Menu Selection | C → S | Variable | Which button the player pressed, which switches were ticked, and what was typed into each text field. |
| [`0xDD`](../outgoing/0xdd-compressed-gump.md) | Compressed Gump | S → C | Variable | The layout command string and the block of strings it indexes into, each zlib-deflated. |
