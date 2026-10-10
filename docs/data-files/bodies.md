# Bodies

`bodies.toml` says what kind of creature each body is. There is one list per kind:
`human`, `animal`, `monster`, `sea` and `equipment` (`BodyType`). Each entry is a
single id or an inclusive `"min-max"` range, always quoted:

```toml
human = [
    "183-186", "400-403", "605-608", "666-667", "694-695", "744-745",
    "750-751", "987-988", "990-991", "994", "1253",
]

sea = [
    "144-145", "150-151",
]
```

A body not listed counts as `Empty`. The loader returns one `BodyContent` per body id,
sorted by id, not one per entry. The combat, the NPC doors and the use requests read the body kinds.

## Validation at startup

The server stops when:

- `bodies.toml` does not exist;
- an entry is not a number or a `"min-max"` range, a range starts after its end, or
  an id is above 0xFFFF;
- a body id is listed under two kinds.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
