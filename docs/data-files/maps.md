# Maps

`maps.toml` lists the facets of the shard:

```toml
[[map]]
map = "felucca"
file_index = 0
name = "Felucca"
size = "(7168, 4096)"
rules = "FeluccaRules"
season = "desolation"
weather = "temperate"
```

| Field | Meaning |
| --- | --- |
| `map` | The map id sent to the client: `felucca`, `trammel`, `ilshenar`, `malas`, `tokuno` or `termur` (`MapType`). |
| `file_index` | The number of the client map files: `map{n}.mul` or `map{n}LegacyMUL.uop`, `staidx{n}.mul` and `statics{n}.mul`. |
| `name` | The name shown in logs and commands. |
| `size` | Width and height in tiles, a `Point2D`. |
| `rules` | The name of the rule set of the map. |
| `season` | The season of packet 0xBC: `spring`, `summer`, `fall`, `winter` or `desolation`. |
| `weather` | The profile of `weather.toml` used where no region covers a place. Defaults to `none`. |
| `music` | The music track, a `MusicType` name such as `Britain1`, played where no region with music covers a place. Left out, the music stops there; no shipped map sets one, so outside the regions it is silent, as in ModernUO. |

The shipped file lists the six maps; Felucca and Trammel use `temperate`, the others
`none`.

At character creation the client reports which maps it has installed as
`ClientFlags` (`src/Moongate.Ultima/Types/ClientFlags.cs`); nothing compares them
with this file yet.

## Validation at startup

The server stops when:

- `maps.toml` does not exist;
- a map's `weather` is not a profile of `weather.toml`. The regions loader makes
  this check, since maps load before the weather profiles;
- the client directory lacks the map, `staidx` or `statics` file of a map's
  `file_index`. `IMapService` makes this check after the loaders; remove the map
  from `maps.toml` when the client has no files for it.

## Read the map from code

`IMapService` reads the terrain and statics of these maps, and `IMovementService` and
`ILineOfSightService` answer movement and sight questions on them; see
[Client files and world queries](../world-queries.md).

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
