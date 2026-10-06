# resurrect

Raises the NPC whose corpse you target, or the ghost of a player you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `resurrect`, then target a corpse or a ghost | No | Yes | GameMaster | Game |

```text
.resurrect
```

In game only. Target the corpse of an NPC: one of the same mobile template is born where the corpse
lies, with the name and the facing of who died, and you read `Tiara is back.` A human, elf or
gargoyle body rises with its fall played backwards. The corpse is gone, with what was left inside.

The NPC comes with the equipment of its template, as a new one would: what was taken from the
corpse stays taken, and what was left in it is not given back. One that belonged to a spawn region
counts for it again. See [Death and resurrection](../death.md#raising-who-died).

Target a dead player instead and it is raised where it stands, as by an ankh: 10 hit points, full stamina, no mana, and a death robe in the place of its shroud. See [Death of a player](../death.md#death-of-a-player).

- Not a corpse lying on the ground, nor a ghost: `That is not a corpse.`
- A corpse of an NPC that had no template, or whose template is gone, or that someone is already
  raising: `That corpse cannot be raised.`

## See also

- [All commands](../commands.md)
- [Death and resurrection](../death.md)
- [`kill`](kill.md)
