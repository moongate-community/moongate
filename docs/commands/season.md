# season

Shows the season where you stand, or sets the season of your map.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `season [spring\|summer\|fall\|winter\|desolation\|auto]` | No | Yes | GameMaster | Game |

```text
.season
.season winter
.season auto
```

In game only. Without an argument it prints the season the client shows where you stand (the
region's, else the map's) and the season of your map: `Season here: fall; trammel: summer.`

With a season it sets the season of your map until the restart and sends it at once to every player
on the map, except those in a region with a season of its own: `Season of trammel: winter.` `auto`
gives the map back its `maps.toml` season, rotated when `[ultima.world] season_rotation` is on. See
[Seasons](../data-files/maps.md#seasons).

The client draws the season itself: leafless trees and snow in winter, autumn colours in fall,
dead trees in desolation. It plays a sound on the change.

## See also

- [All commands](../commands.md)
- [`time`](time.md)
- [`weather`](weather.md)
