# tame

Gives the creature you target to yourself, or to a character you name. The owner may ride it.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `tame [name]`, then target a creature | No | Yes | GameMaster | Game |

```text
.tame
.tame Aria
```

In game only. Target a creature that can be ridden (see [Mounts](../mounts.md#making-a-creature-rideable)). With no
name you become its owner and read `a horse now belongs to Giachi.`; with the name of a character in the world, in any
case, the character does.

- A creature with no mount item, or a player: `an orc cannot be tamed.`
- A name nobody has: `No character is named Nobody.`
- An item, or a mobile that is gone: `That is not an NPC.`

## See also

- [All commands](../commands.md)
- [Mounts](../mounts.md)
