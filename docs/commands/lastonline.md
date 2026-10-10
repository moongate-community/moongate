# lastonline

Prints when a player character was last online, whether it is in the world or not.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `lastonline <name>` | Yes | Yes | GameMaster | Game |

```text
.lastonline Aria
Aria was last online on 2026-10-09 18:05 (UTC).
```

The name is looked up without case, and a name of several words is read as one. A character in the
world answers `Aria is online now.` Several characters of one name are all listed; a character
pending deletion is left out, and a name nobody has prints `No character is named x.`

The date is written when the character leaves the world (a closed connection, a character change or a
server stop), in UTC. A character that has not left the world since the date began to be recorded has
none yet: `Aria has no last online date yet: it has not left the world since the date began to be recorded.`
