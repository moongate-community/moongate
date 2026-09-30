# spawns

Lists the spawn regions where you stand, with their live NPCs and the minutes to their next spawn.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `spawns` | No | Yes | GameMaster | Game |

```text
.spawns
```

In game only. It prints one line per spawn region with an area over your position, in file
order:

```text
The Hammer And Anvil (felucca_0): 1/1 NPCs, next spawn in 312 min.
```

The name comes first (the id when the region has none), then the id, the live NPCs against the
region's `max`, and the minutes until the next check spawns there. A region at its `max` still has
a next spawn: it spawns nothing then and waits again. `No spawn region here.` means no region
covers the spot. See [NPC lists and spawns](../templates.md#npc-lists-and-spawns).

## See also

- [All commands](../commands.md)
- [`spawn`](spawn.md)
- [`remove`](remove.md)
