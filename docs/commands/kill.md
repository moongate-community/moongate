# kill

Kills the NPC or the player you target: it dies where it stands and leaves its corpse.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `kill`, then target an NPC | No | Yes | GameMaster | Game |

```text
.kill
```

In game only. Target an NPC or a player: it dies as when something kills it, with its corpse, the death seen
and heard around and its script told, and you read `an orc is dead.` You are kept as its killer on
the corpse. See [Death and resurrection](../death.md) for what the corpse holds and how long it lies.

- A player who is dead already, or whose body has no ghost: `Aria cannot die.` A player dies as an NPC does, but stays as a ghost: see [Death of a player](../death.md#death-of-a-player).
- An item, or a mobile that is gone: `That is not an NPC.`

To take an NPC away without a corpse, use [`remove`](remove.md).

## See also

- [All commands](../commands.md)
- [Death and resurrection](../death.md)
- [`resurrect`](resurrect.md)
- [`remove`](remove.md)
- [`spawn`](spawn.md)
