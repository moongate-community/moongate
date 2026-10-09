# Locations

`locations.toml` lists the named places staff travels to: [`.go`](../commands/go.md) alone opens a
gump that lists them by map and category, and `.go <place>` goes to one by name.

```toml
[[location]]
map = "felucca"
category = "Dungeons/Covetous"
name = "Entrance"
location = "(2499, 919, 0)"

[[location]]
map = "malas"
category = ""
name = "Arena"
location = "(1, 2, -3)"
```

| Field | Meaning |
| --- | --- |
| `[[location]]` | One per place; the order is the order of the gump. |
| `map` | `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` or `termur`. |
| `category` | Where the gump files the place: the categories from the map down, joined by `/`. Empty, or left out, for a place listed under the map itself. |
| `name` | The name shown. Names may repeat: `.go` tells them apart by the words of the category. |
| `location` | Where the traveller arrives, a `Point3D`. |

A category exists because a place names it: there is nothing else to declare. Categories that
differ only by case are one, spelled as the first place spells it.

The shipped file holds the 558 places of ModernUO's `[Go` gump, on the six maps. Write it again
from a ModernUO checkout with
[`moongate-convert modernuo-locations`](../uox3-migration.md#named-places-of-modernuo).

A place of a map the server does not load is left out, and so is one outside its map. The file
may be missing: `.go` then takes numbers only.

Scripts read the places with `locations.node(path)` and `locations.find(text, map)`; see
the [`locations` module](https://moongate.sh/lua/locations/).

## Validation at startup

The server stops at startup when:

- a `[[location]]` has no `name`, no `map` or an unknown one;
- its `location` is missing or `(0, 0, 0)`, or has `x` or `y` outside 0 to 65535 or `z` outside -128 to 127;
- its `category` has an empty part, such as `Dungeons//Covetous`.

## See also

- [`go`](../commands/go.md): the command that lists the places and travels to them.
- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
