# Movement

Move requests, acks, and mobile position updates.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x02`](../incoming/0x02-move-request.md) | Move Request | C → S | 7 bytes (fixed) | One step or turn, with anti-fastwalk key. |
| [`0x20`](../outgoing/0x20-draw-game-player.md) | Draw Game Player | S → C | 19 bytes (fixed) | Positions and renders the player's own mobile. |
| [`0x21`](../outgoing/0x21-char-move-rejection.md) | Char Move Rejection | S → C | 8 bytes (fixed) | Echoes the rejected sequence with the mover's true position, forcing a client snap-back. |
| [`0x22`](../outgoing/0x22-movement-ack.md) | Movement Ack | S → C | 3 bytes (fixed) | Confirms the step with the client's sequence number. |
| [`0x77`](../outgoing/0x77-update-player.md) | Update Player | S → C | 17 bytes (fixed) | Broadcasts another mobile's position/facing to nearby players. |
| [`0x78`](../outgoing/0x78-draw-object.md) | Draw Object | S → C | Variable | Draws a mobile and its equipped items on the client. |
