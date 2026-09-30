# spawn

Spawns an NPC from a mobile template where you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `spawn <template>`, then target a spot | No | Yes | GameMaster | Game |

```text
.spawn orione
```

In game only. The template id is checked first (`Unknown mobile template: <id>` otherwise);
then a target cursor opens and the NPC appears on the spot you pick, dressed and with its
loot, and runs its Lua `on_spawn`: `Spawned Orione (0x00000123) at Trammel (1496, 1628, 10).`
The NPC is saved with the world. A failure prints `The spawn failed. Check the server logs.`
The mobile templates are in `templates/mobiles` (see [Loading TOML templates](../templates.md)).

## See also

- [All commands](../commands.md)
- [`remove`](remove.md)
- [`where`](where.md)
