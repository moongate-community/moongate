# Status & skills

Mobile status, paperdoll, skills, war mode, stat/skill locks.

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x11`](../outgoing/0x11-mobile-status.md) | Mobile Status | S → C | — | The player's own status window. |
| [`0x3A`](../incoming/0x3a-skill-lock-change.md) | Skill Lock Change | C → S | Variable | The client sets the up/down/lock arrow on one skill. |
| [`0x3A`](../outgoing/0x3a-skills.md) | Skills | S → C | Variable | Skill list (0x3A), in the absolute-with-caps form (type 0x02): the client's whole skill list in one go. |
| [`0x72`](../outgoing/0x72-war-mode.md) | War Mode | S → C | 5 bytes (fixed) | Toggles the client's combat stance. |
| [`0x88`](../outgoing/0x88-paperdoll.md) | Paperdoll | S → C | 66 bytes (fixed) | Tells the client to open the character window for a mobile. |
| [`0xBF`](../outgoing/0xbf-stat-lock-info.md) | Stat Lock Info | S → C | 12 bytes (fixed) | Stat lock info (0xBF sub-command 0x19): the up/down/lock state of the three stats, packed two bits each into a single byte. |
