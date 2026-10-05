# create_check

Puts in your backpack a bank check worth the gold you say.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `create_check <1..2000000000>` | No | Yes | GameMaster | Game |

```text
.create_check 2000
```

In game only. The check is made out of nothing, no bank pays for it, and it lands in your
backpack: `A bank check worth 2,000 gold is in your backpack.` It is the same item a banker
writes ([bank checks](../bank.md#bank-checks)): put it in a bank box and double click it there
to turn it into coins.

- The amount is a whole number from 1 to 2,000,000,000, with no separators: the smallest and
  largest check of a banker ([`min_check`, `max_check`](../bank.md#settings)) do not hold you.
- With no backpack, or a full one, nothing is made: `No check was made: you have no backpack,
  or it is full.`

## See also

- [All commands](../commands.md)
- [`add_gold`](add_gold.md)
- [The bank](../bank.md)
