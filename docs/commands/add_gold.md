# add_gold

Puts a pile of gold in the backpack of the character or NPC you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `add_gold <1..60000>`, then target a mobile | No | Yes | GameMaster | Game |

```text
.add_gold 1000
```

In game only. The amount is checked first; then a target cursor opens and the character or NPC
you pick, yourself included, gets a new pile of that many coins in its backpack, made out of
nothing: `1,000 gold is in the backpack of Bran.`

- One pile holds 60000 coins, so that is the most at one time. For a larger sum make a
  [bank check](create_check.md).
- Gold weighs, a coin 0.02 stones: the one who gets it may walk away overloaded.
- Picking an item says `That is not a character or an NPC.`; cancelling changes nothing; a
  mobile with no backpack, or a full one, gets nothing: `Bran has no backpack, or it is full.`

## See also

- [All commands](../commands.md)
- [`create_check`](create_check.md)
- [`add`](add.md)
