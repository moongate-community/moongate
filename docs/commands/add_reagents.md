# add_reagents

Puts the reagents of a spell, of a circle or of all the spells in your backpack.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `add_reagents <key \| number \| circle N \| all> [amount]` | No | Yes | GameMaster | Game |

```text
.add_reagents recall
.add_reagents circle 4 100
.add_reagents all 500
```

In game only; there is no target cursor. The reagents are the ones the spells name in
[`spells.toml`](data-files/spells.md), one stack for each kind of reagent, `amount` of each: 20 when you leave it out,
from 1 to 1000. A reagent that several spells use is given once. The command answers what it gave:
`Reagents in your backpack, 20 of each: black pearl, blood moss, mandrake root.`

- The spell is named as in [`add_spell`](add_spell.md): by key, by number, `circle N` or `all`. With `all` you get the eight
  classic reagents.
- A stack joins the stack of its kind already in your backpack, as a drop would.
- A stack the backpack has no room for, by items or by weight, is put on the ground at your feet, and the command says so:
  `20 of each did not fit the backpack and lie at your feet: garlic.`
- An unknown spell or circle, or an amount outside 1 to 1000, is answered before anything is made.

## See also

- [All commands](../commands.md)
- [`add_spell`](add_spell.md)
- [Magery](../magery.md)
