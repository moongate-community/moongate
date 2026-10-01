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
| `season` | The season of packet 0xBC: `spring`, `summer`, `fall`, `winter` or `desolation`; the starting point of the rotation, see [Seasons](#seasons). |
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

## Seasons

The client draws the season itself: green trees and flowers in spring, the usual look in summer,
autumn colours in fall, leafless trees and snow in winter, dead trees and grey land in desolation.
Nothing else changes: the weather stays the region's.

A player sees the season of its region when the region sets one (`season` in
[`data/regions`](regions.md)), else its map's. The map's season is:

1. the one a game master set with [`.season`](../commands/season.md), until the restart;
2. else `season` here. With `[ultima.world] season_rotation = true` it rotates spring, summer, fall,
   winter, starting from that season and changing every `days_per_season` game days (12 by default,
   a real day at the default game minute). A `desolation` map, Felucca as shipped, never rotates. No
   other emulator rotates the seasons, so it is off by default.

The season is sent (0xBC) at login, when a player walks into a region with another season, and
within a minute of a rotation, and at once on `.season`; the light and the weather follow it,
since the client resets them.

## See also

- [Data files overview](../data-files.md): file locations, loading order and shared value formats.
- [Check your changes](../data-files.md#check-your-changes): validate edited data before restarting.
