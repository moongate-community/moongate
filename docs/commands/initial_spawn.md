# initial_spawn

Fills every spawn region in the world to its `max` at the next spawn check.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `initial_spawn` | Yes | Yes | Administrator | Game |

```text
.initial_spawn
```

It takes no arguments. It marks every spawn region to fill at once and moves its next spawn to
now, then says how many regions and NPCs that is:

```text
Filling 4041 spawn regions: 29000 NPCs to spawn. The spawn messages show the progress.
```

The NPCs arrive at the next check of the `npc_spawn` timer, within 10 seconds, each region bringing
what it misses to its `max`, whatever its `call` and `[ultima.spawns] initial_fill`. The staff spawn
messages and the server log then show how full the world is. A region that finds no spot for some
NPCs tries again a minute later and keeps filling until it is full; after that it goes back to its
`call` and its times. Use it on a new world, or after removing many NPCs, instead of waiting for the
gradual fill. See [NPC spawns](../spawns.md).

## See also

- [All commands](../commands.md)
- [`spawns`](spawns.md)
- [`decorate`](decorate.md)
