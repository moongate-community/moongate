# chat

Speech and system messages. A mobile speaks by serial; a broadcast reaches every
in-world session.

## chat.say

```lua
chat.say(serial, text) -> boolean
```

Speaks as the mobile with that serial. Returns `false` — and says nothing — when
the serial is unknown, the text is blank, the text is longer than **128
characters**, or the text is a command rather than speech.

The leading character decides how it is spoken:

| prefix | result | range |
|---|---|---|
| `.` | **command — refused**, never spoken | — |
| `*…*` | emote, the asterisks stripped | 15 |
| `!` | yell | 18 |
| `;` | whisper | 1 |
| anything else | ordinary speech | 15 |

**Example**

```lua
chat.say(npc, "Welcome back.")     -- ordinary, heard 15 tiles away
chat.say(npc, "*nods*")            -- emote, spoken as "nods"
chat.say(npc, "!Guards!")          -- yelled, heard 18
chat.say(npc, ".help")             -- false, nothing is said
```

## chat.broadcast

```lua
chat.broadcast(text)
```

Sends a system message to every in-world session, with no range check and no
speaker. Returns nothing.

**Example**

```lua
chat.broadcast("The shard restarts in five minutes.")
```

## See also

[`ai.say`](ai.md) speaks as the NPC a brain is running for, with no serial to
pass. It applies these same rules — both go through one service, so they cannot
drift apart.
