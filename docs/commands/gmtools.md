# gmtools

Opens the gump of the game master's tools: a sidebar of tools on the left (weather and season) and the
commands of the selected tool on the right.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `gmtools` | No | Yes | GameMaster | Game |

```text
.gmtools
```

In game only. The gump opens on the first tool of the sidebar; a click on another tool shows its
panel.

## Weather

The weather tool shows the weather where you stand, the profile of the place and what it does now:

```text
Weather here: temperate
Now: rain, density 40, temperature 12
```

Under it are four buttons, `none`, `rain`, `snow` and `storm`. A click forces that weather on the
profile of the place, as [`.weather`](weather.md) does: every region using the profile gets it, until
the next game hour rolls it again. You read `The weather of temperate is now storm until the next
hour.` and the gump shows the new state.

There is no choice of profile yet: the weather is the one of the place you stand in, so walk or
[`.go`](go.md) to the place whose sky you want to change.

## Season

The season tool shows the season the client shows where you stand (the region's, else the map's)
and the season of your map:

```text
Season here: winter
Season of your map: summer
```

Under it are six buttons, `spring`, `summer`, `fall`, `winter`, `desolation` and `auto`. A click sets
the season of your map until the restart and sends it at once to every player on the map, as
[`.season`](season.md) does, except those in a region with a season of its own; `auto` gives the map
back its `maps.toml` season, rotated when `[ultima.world] season_rotation` is on. You read `The
season of your map is now winter.` and the gump shows the new state. The client draws the season by
itself, with a sound on the change: leafless trees and snow in winter, autumn colours in fall.

The season is the look of the ground; the snow that falls is the weather, which the first tool sets.
For a snowy world, set both.

## Add a tool

The gump is [`templates/gumps/gmtools.xml`](../gumps.md) and its script
[`scripts/gumps/gmtools.lua`](../scripting/shipped-scripts.md#gmtoolslua), which are yours to change.
A tool is one entry of the table `tools` in the script and a function that draws its panel. Anyone
who is not a game master sees an empty gump, and a button of the gump does nothing for one who has
lost the rank since it was opened.

## See also

- [All commands](../commands.md)
- [`weather`](weather.md)
- [`season`](season.md)
- [`go`](go.md)
