# set

Sets the hits, mana, stamina, hunger or thirst of the mobile you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `set <hits\|mana\|stamina\|hunger\|thirst> <value>`, then target a mobile | No | Yes | GameMaster | Game |

```text
.set hits 10
.set hunger 0
.set thirst 0
```

In game only. A target cursor opens: pick a character or an NPC, yourself included. Hit points, mana
and stamina are kept between 0 and the mobile's maximum, and its player and those around see the bar
move; hunger is kept between 0 (starving) and 20 (full), thirst between 0 (parched) and 20 (quenched). The command answers with what the number is
now: `Aria: hits is now 10.` Targeting an item prints `That is not a character or an NPC.`

It is the way to watch the [regeneration](../server-configuration.md) at work: lower a bar and it comes
back a point at a time; set hunger to 0 and the hit points stay where they are until the mobile eats;
set thirst to 0 and the stamina stays where it is until the mobile drinks.

## See also

- [All commands](../commands.md)
- [`fame`](fame.md)
- [`karma`](karma.md)
