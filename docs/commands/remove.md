# remove

Removes the NPC or the item on the ground you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `remove`, then target an NPC or an item on the ground | No | Yes | GameMaster | Game |

```text
.remove
```

In game only. Target an NPC: it disappears for everyone, with what it wears and carries, and
its row is deleted by the next world save: `Removed 0x00000123.` Targeting a player character
prints `That is not an NPC.` and removes nothing.

Target an item lying on the ground, such as a chest put there with [`add`](add.md): it disappears
for everyone with everything inside it, at any depth, and the next world save deletes the rows:
`Removed 0x40001234.` An item of a spawn region frees its place, so the region makes another at its
next time. An item that is carried, worn or inside a container is left where it is:
`Only an item lying on the ground can be removed.`

## See also

- [All commands](../commands.md)
- [`spawn`](spawn.md)
- [`add`](add.md)
