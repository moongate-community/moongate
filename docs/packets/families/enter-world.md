# Enter world

Login confirm, feature flags, and the login-complete marker.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x1B`](../outgoing/0x1b-login-confirm.md) | Login Confirm | S → C | 37 bytes (fixed) | The first packet of the enter-world burst. |
| [`0x55`](../outgoing/0x55-login-complete.md) | Login Complete | S → C | 1 bytes (fixed) | The "you are now in the world" marker that unblocks the client. |
| [`0xB9`](../outgoing/0xb9-support-features.md) | Support Features | S → C | 5 bytes (fixed) | Unlocks the client feature set at login, sent right before the character list. |
