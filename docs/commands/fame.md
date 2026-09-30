# fame

Sets the fame of the character or NPC you target.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `fame <0..32000>`, then target a mobile | No | Yes | GameMaster | Game |

```text
.fame <0..32000>
.karma <-32000..32000>
```

In game only. The value is checked first; then a target cursor opens and the character or NPC
you pick gets it: `Bran now has 10000 fame.` Picking an item or cancelling changes nothing. The
paperdoll title follows at once: the [fame and karma prefix](../data-files/titles.md), which says
`Lord` or `Lady` from 10,000 fame. The value is saved with the mobile by its next save.

## See also

- [All commands](../commands.md)
- [`karma`](karma.md)
