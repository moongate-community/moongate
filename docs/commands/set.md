# set

Sets the hits, mana, stamina or hunger of the mobile you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `set <hits\|mana\|stamina\|hunger> <value>`, then target a mobile | No | Yes | GameMaster | Game |

```text
.set hits 10
.set hunger 0
```

In game only. A target cursor opens: pick a character or an NPC, yourself included. Hit points, mana
and stamina are kept between 0 and the mobile's maximum, and its player and those around see the bar
move; hunger is kept between 0 (starving) and 20 (full). The command answers with what the number is
now: `Aria: hits is now 10.` Targeting an item prints `That is not a character or an NPC.`

It is the way to watch the [regeneration](../server-configuration.md) at work: lower a bar and it comes
back a point at a time; set hunger to 0 and the hit points stay where they are until the mobile eats.

## See also

- [All commands](../commands.md)
- [`fame`](fame.md)
- [`karma`](karma.md)
