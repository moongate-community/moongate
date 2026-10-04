# Jail

`jail.toml` describes the [jail](../jail.md): its map and its cells. The gump of
[`.jail`](../commands/jail.md) lists the cells by number, and a prisoner arrives on the location
of its cell.

```toml
map = "felucca"
release = "(1444, 1697, 10)"

[[cell]]
number = 1
location = "(5276, 1164, 0)"

[[cell]]
number = 2
location = "(5286, 1164, 0)"
```

| Field | Meaning |
| --- | --- |
| `map` | The map of the cells: `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` or `termur`. |
| `release` | Where a released prisoner goes when the map it was arrested on is no longer loaded, a `Point3D` on the jail's map. |
| `[[cell]]` | One per cell, in the order the gump lists them. |
| `number` | The number the gump shows; from 1, each used once. |
| `location` | Where the prisoner arrives, a `Point3D`. |

The shipped file holds the ten cells of Felucca's jail, the same ModernUO and UOX3 use, and
releases to Britain's bank as ModernUO does. Eight cells are closed rooms of nine tiles by nine,
the last two are twice as wide.

A cell added here needs its chest of rations: one more region in
`templates/spawns/felucca/jail.toml`, as [Jail](../jail.md#the-chest-of-rations) explains.

The file may be missing: the jail is then off and `.jail` says so.

Scripts read the cells with `jail.cells()`; see the [`jail` module](https://moongate.sh/lua/jail/).

## Validation at startup

The server stops at startup when the file exists and:

- it has no `map` or an unknown one;
- it has no `release`, or one with `x` or `y` outside 0 to 65535 or `z` outside -128 to 127;
- it has no `[[cell]]`;
- a cell has a `number` below 1 or one that another cell has;
- a cell has no `location`, or one outside the same limits.

## See also

- [Jail](../jail.md): what the jail does.
- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
