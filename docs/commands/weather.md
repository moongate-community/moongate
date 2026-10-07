# weather

Shows the weather where you stand, or forces it until the next game hour.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `weather [none\|rain\|snow\|storm]` | No | Yes | GameMaster | Game |

```text
.weather
.weather storm
```

In game only. Without a kind it prints the weather where you stand:
`Weather here (temperate): rain, density 40, temperature 12.` With `none`, `rain`, `snow` or
`storm` it forces that weather on the profile of the place (every region using the profile
gets it) until the next game hour rolls it again. See the
[weather profiles](../data-files/weather.md).

## See also

- [All commands](../commands.md)
- [`gmtools`](gmtools.md): the same buttons in a gump
- [`globallight`](globallight.md)
