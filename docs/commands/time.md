# time

Prints the game time where you stand.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `time` | No | Yes | Regular | Game |

```text
.time
```

In game only, and for every player. It takes no arguments and prints the hour and minute of the
game clock at your position:

```text
Game time here: 07:05.
```

The game clock is the one that drives day and night: a game minute lasts
`[ultima.world] seconds_per_uo_minute` real seconds (5 by default, so a game day is 2 real hours),
each map runs 320 game minutes after the previous one, and the time moves one minute later every 16
tiles east. Two players on the same map can so see different times.

## See also

- [All commands](../commands.md)
- [`globallight`](globallight.md)
