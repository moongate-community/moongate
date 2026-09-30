# remove

Removes the NPC you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `remove`, then target an NPC | No | Yes | GameMaster | Game |

```text
.remove
```

In game only. Target an NPC: it disappears for everyone, with what it wears and carries, and
its row is deleted by the next world save: `Removed 0x00000123.` Targeting a player
character, an item or a spot prints `That is not an NPC.` and removes nothing.

## See also

- [All commands](../commands.md)
- [`spawn`](spawn.md)
