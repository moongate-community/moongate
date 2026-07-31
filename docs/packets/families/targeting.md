# Targeting

The target cursor: the server asking the player to click something, and the answer.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x6C`](../incoming/0x6c-target-cursor-response.md) | Target Cursor Response | C → S | 19 bytes (fixed) | What the player clicked. |
| [`0x6C`](../outgoing/0x6c-target-cursor.md) | Target Cursor | S → C | 19 bytes (fixed) | Raises the client's targeting cursor, or takes it down when <paramref name="CursorType" /> is `Cancel`. |
