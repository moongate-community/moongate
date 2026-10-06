# time

Prints the game time where you stand and the phases of the two moons.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `time` | No | Yes | Regular | Game |

```text
.time
```

In game only, and for every player. It takes no arguments and prints the hour and minute of the
game clock at your position, then the phases of Trammel and Felucca, as ModernUO's spyglass shows
them:

```text
Game time here: 07:05.
Moons: Trammel last quarter, Felucca first quarter.
```

The game clock is the one that drives day and night: a game minute lasts
`[ultima.world] seconds_per_uo_minute` real seconds (5 by default, so a game day is 2 real hours),
each map runs 320 game minutes after the previous one, and the time moves one minute later every 16
tiles east. Two players on the same map can so see different times.

Each moon goes through 8 phases (new moon, waxing crescent, first quarter, waxing gibbous, full
moon, waning gibbous, last quarter, waning crescent) on its own map's clock: Felucca's turns every 10
game minutes, Trammel's every 30, and both move with the longitude like the time.

## See also

- [All commands](../commands.md)
- [`globallight`](globallight.md)
- [`gmtools`](gmtools.md): the time, the moons and the light in a gump
