# mark_rune

Marks the recall rune you target with the place where you stand.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `mark_rune`, then target a rune | No | Yes | GameMaster | Game |

```text
.mark_rune
```

In game only. A target cursor opens: pick a recall rune (the item `recall_rune`), in your backpack or on the ground. The rune
keeps your map and place (`rune.x`, `rune.y`, `rune.z`, `rune.map`) and takes the name of the region you stand in, `a recall
rune for Britain`, or of the map outside every region. The command answers `The rune is marked for Britain.`; another item
answers `That is not a recall rune.`

The [Recall spell](../magery.md#recall-and-runes) carries its caster to the place of a marked rune, unless a region forbids it. A
rune can be marked again: it holds the last place.

## See also

- [All commands](../commands.md)
- [Magery](../magery.md)
