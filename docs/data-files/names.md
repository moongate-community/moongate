# Names

`names.toml` holds the lists random NPC names are drawn from. A mobile template names a
list with `name_list`; `INameService.RandomName(listId)` picks one name from it.

```toml
[[names]]
id = "male"
names = [
    "Aaron",
    "Abbott",
]
```

| Field | Meaning |
| --- | --- |
| `id` | The list id, unique ignoring case |
| `names` | The names |

The shipped file has UOX3's twenty lists (`namelists.dfn`), converted by
[`mgctl convert uox`](../uox3-migration.md#mobiles-and-name-lists): `male`, `female`, `orc`,
`daemon`, `ratman` and so on. The loader trims ids and names and returns one `NameList` per list.

## Validation at startup

The server stops when:

- `names.toml` does not exist;
- a list id is empty, or used twice ignoring case;
- a list is empty, or a name is empty after trimming.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
