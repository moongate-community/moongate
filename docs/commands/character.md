# character

Lists the characters waiting for deletion and restores them.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `character pending [account-serial]` / `character restore <character-serial>` | Yes | Yes | GameMaster | Game |

```text
character pending [account-serial]
character restore <character-serial>
```

A character a player deletes from the character list is only marked for deletion:
it disappears from the list, gives up its slot and no longer counts toward
`ultima.characters.max_per_account`, so the player can create a new character in its
place. It stays restorable until it is removed; after
`ultima.characters.deletion_delay_hours` (default 24) it becomes eligible for removal,
by a job that is not built yet. `character pending` lists every
pending character, or those of one account, with when the deletion was requested
and when the character becomes eligible for removal. `character restore` cancels
the deletion and gives the character the first free slot; if the account filled
up meanwhile it stays without a slot, may exceed the limit by one, and appears in
the list once a slot frees. Serials
are hexadecimal with `0x` (`0x0000002A`) or decimal.

## See also

- [All commands](../commands.md)
