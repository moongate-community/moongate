# resurrect

Raises the NPC whose corpse you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `resurrect`, then target a corpse | No | Yes | GameMaster | Game |

```text
.resurrect
```

In game only. Target the corpse of an NPC: one of the same mobile template is born where the corpse
lies, with the name and the facing of who died, and you read `Tiara is back.` A human, elf or
gargoyle body rises with its fall played backwards. The corpse is gone, with what was left inside.

The NPC comes with the equipment of its template, as a new one would: what was taken from the
corpse stays taken, and what was left in it is not given back. One that belonged to a spawn region
counts for it again. See [Death of NPCs](../death.md#raising-who-died).

- Not a corpse lying on the ground: `That is not a corpse.`
- A corpse of an NPC that had no template, or whose template is gone, or that someone is already
  raising: `That corpse cannot be raised.`

## See also

- [All commands](../commands.md)
- [Death of NPCs](../death.md)
- [`kill`](kill.md)
