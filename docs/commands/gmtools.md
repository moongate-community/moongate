# gmtools

Opens the gump of the game master's tools: a sidebar of tools on the left (weather, season, time and, for administrators, events) and the
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

## Time

The time tool shows what [`.time`](time.md) prints, the game time and the phases of the two moons at
your position, and the light level where you stand:

```text
Game time here: 07:05
Moons: Trammel last quarter, Felucca first quarter
Light here: 0, following the time of day
```

Under it are five buttons, the levels `0 (brightest)`, `12`, `26` and `31 (darkest)`, and `auto (the
time of day)`. A level gives every player in the world that light at once, as
[`.globallight`](globallight.md) does, and the line says `Light here: 26, the same for every player`;
`auto` goes back to the light of the time of day. You read `The global light is now 26.` or `The
global light follows the time of day again.` The override is not saved: a restart goes back to the
time of day. For a level between the buttons, use `.globallight <0-31>`.

## Events

The events tool is for administrators, as [`.event`](event.md) is; a game master does not see it. It lists the
[seasonal events](../schedule.md#seasonal-events) with their dates, mode and state:

```text
Seasonal events
Halloween (10-24 to 11-15): auto, on
Winter (12-20 to 01-06): auto, off
```

Under each event are three buttons, `auto`, `on` and `off`: `auto` gives the event back to its dates, `on` and `off`
force it. You read `Halloween is now off.` and the gump opens again; the event starts or ends as it does with
the command, and its mode is kept across restarts. The panel lists four events; the others are left to `.event`.

## Add a tool

The gump is [`templates/gumps/gmtools.xml`](../gumps.md) and its script
[`scripts/gumps/gmtools.lua`](../scripting/shipped-scripts.md#gmtoolslua), which are yours to change.
A tool is one entry of the table `tools` in the script and a function that draws its panel. Anyone
who is not a game master sees an empty gump, and a button of the gump does nothing for one who has
lost the rank since it was opened. A tool with `admin = true` in the table is only for administrators.

## See also

- [All commands](../commands.md)
- [`weather`](weather.md)
- [`season`](season.md)
- [`time`](time.md) and [`globallight`](globallight.md)
- [`event`](event.md)
- [`go`](go.md)
