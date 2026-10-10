# Starting cities

`starting_cities.toml` lists the cities a new character can start in. The game
server sends them with the character list (packet 0xA9) right after the game login.
The client sends back the index of the chosen city, so the order of the entries
matters. The help module reads the same cities to send a stuck player ("I am stuck")
to the nearest one.

```toml
[[starting_city]]
town = "New Haven"
description = "The Bountiful Harvest Inn"
location = "(3503, 2574, 14)"
map = "trammel"
cliloc = 1150168
```

| Field | Meaning |
| --- | --- |
| `town` | The city name the client shows. |
| `description` | The place in the city, such as an inn. |
| `location` | Where the character appears, a `Point3D`. |
| `map` | The map of `location`. |
| `cliloc` | The id of the localized description the client shows. |

## Validation at startup

The loader checks the limits of the character list packet, so a bad city stops the
server at startup instead of failing at each game login. It stops when:

- the file does not exist;
- it has no `[[starting_city]]` entries, or more than 255;
- a `town` or `description` is empty or blank, longer than 32 characters or not ASCII.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
