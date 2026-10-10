# add_spell

Writes a spell, the spells of a circle or all 64 in the spellbook you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `add_spell <key \| number \| circle N \| all>`, then target a spellbook or a mobile | No | Yes | GameMaster | Game |

```text
.add_spell magic_arrow
.add_spell 37
.add_spell circle 3
.add_spell all
```

In game only. The spells are checked first; then a target cursor opens. Pick a spellbook, in a backpack or on the ground,
or a character or an NPC: it gets the spells in the spellbook it wears, or else the first one in its backpack (not in a bag
inside it), the same lookup a cast does. The command answers how many spells were new:
`Spells added to the spellbook: 4 new, 12 in the book now.`

- A spell is named by its key (`magic_arrow`, case does not matter) or by its number, 1 to 64, as the client counts
  them, the ones of [`spells.toml`](data-files/spells.md). `circle N` is the eight spells of the circle N, 1 to 8, and
  `all` is the 64.
- The spells come from the catalog: one with no script yet is added all the same, and the book holds it, though it cannot
  be cast.
- A spell the book holds already is not counted. The book is shown again to its owner, so an open spellbook gains the
  spells at once.
- An unknown spell answers `Unknown spell: <word>`, a circle outside 1 to 8 answers
  `Unknown circle: <word>. A circle is a number from 1 to 8.`, and neither opens a cursor.
- A mobile with no spellbook answers `Bran carries no spellbook.`; any other item answers
  `That is not a spellbook, a character or an NPC.`; cancelling changes nothing.

## See also

- [All commands](../commands.md)
- [`add_reagents`](add_reagents.md)
- [Magery](../magery.md)
