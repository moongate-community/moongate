# add

Puts an item from an item template on the ground where you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `add <template>`, then target a spot | No | Yes | GameMaster | Game |

```text
.add treasure_chest_level_1
```

In game only. The template id is checked first (`Unknown item template: <id>` otherwise);
then a target cursor opens and the item appears on the spot you pick:
`Added treasure chest (0x40001234) at Trammel (1385, 1490, 10).` A container comes with the gold
and the loot its template names, as a treasure chest of a spawn region does, and its script runs
`on_create`. The item is saved with the world and decays as its template says: a treasure chest
after 45 minutes, a fixed item without a decay time never. A stack comes as one item of amount 1.
A failure prints `The item could not be added. Check the server logs.`
The item templates are in `templates/items` (see [Loading TOML templates](../templates.md)). For a
chest that comes back after it is looted, use a [spawn region of items](../spawns.md#regions-of-items-treasure-chests).

## See also

- [All commands](../commands.md)
- [`spawn`](spawn.md)
- [`where`](where.md)
