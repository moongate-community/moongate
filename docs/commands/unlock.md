# unlock

Unlocks the door you target and the door linked to it.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `unlock`, then target a door | No | Yes | GameMaster | Game |

```text
.lock
.unlock
```

In game only. A target cursor opens; the door you pick, and the door linked to it (a double
door), gets or loses the prop `locked`: `The door is now locked.` A locked closed door does not
open for players, who read "That is locked."; game masters and administrators still open it
(see the [door script](../scripting/shipped-scripts.md#doorlua)). Locking also gives both doors a key number (prop
`key.value`) if they have none. Picking anything that is not a door prints
`That is not a door.` The lock is saved with the door by the next world save.

## See also

- [All commands](../commands.md)
- [`lock`](lock.md)
- [`key`](key.md)
