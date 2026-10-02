# Moongates

`moongates.toml` lists the public moongates: the gates between the cities of every map.
[`.decorate`](../commands/decorate.md) places a gate on each destination, and a player who walks
onto one, or double clicks it from the next cell, picks where to go from a gump with one page
per map.

```toml
[[facet]]
map = "malas"
cliloc = 1060643
selected_cliloc = 1062039

[[facet.destination]]
name = "Luna"
cliloc = 1060641
location = "(1015, 527, -65)"

[[facet.destination]]
name = "Umbra"
cliloc = 1060642
location = "(1997, 1386, -85)"
hue = 0x0497
```

| Field | Meaning |
| --- | --- |
| `[[facet]]` | One per map; the order is the order of the tabs in the gump. |
| `map` | `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` or `termur`. |
| `cliloc` | The client text of the map's name in the gump. |
| `selected_cliloc` | The same name as the gump shows it for the open page. |
| `[[facet.destination]]` | The cities of the map, in the order the gump lists them. |
| `name` | The city, for the logs; players see the client text. |
| `cliloc` | The client text of the city's name. |
| `location` | Where the gate stands and travellers arrive, a `Point3D`. |
| `hue` | The hue of the gate; none when left out. |
| `average_z` | `true` takes the height from the map instead of the `z` of `location`, for a spot whose height differs between client versions, such as Magincia. |

The shipped file holds ModernUO's 34 destinations: 9 in Trammel, 9 in Felucca, 9 in Ilshenar,
2 in Malas, 3 in Tokuno and 2 in Ter Mur. The names are client texts, so every player reads
them in the language of the client.

A map the server does not load is left out, with its gates: its page is not in the gump and
`.decorate` places nothing there. A destination outside its map is left out too, with a warning in the log. An empty file
means a shard without public moongates.

Scripts read the list with `moongates.facets()`; see [Scripting](../scripting.md).

## Validation at startup

The server stops at startup when:

- the file does not exist;
- a `[[facet]]` has no `map`, an unknown one, or one that another `[[facet]]` has;
- a facet lacks `cliloc` or `selected_cliloc`, or has no `[[facet.destination]]`;
- a destination lacks `name`, `cliloc` or `location`, its `location` has `x` or `y` outside 0 to 65535 or `z`
  outside -128 to 127, or its `hue` is outside 0 to 65535.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
